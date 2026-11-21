using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Omnikit.Core.Elevation;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>Was Windows über ein geöffnetes Laufwerk selbst sagt (nicht über WMI, sondern direkt vom Gerät).</summary>
internal sealed record DiskFacts(int Number, long Length, int BusType, bool IsRemovable);

/// <summary>
/// Schreibt eine ISO direkt auf den Stick („DD-Modus“). Der Stick wird über seinen Gerätepfad geöffnet, nicht über
/// <c>\\.\PhysicalDriveN</c>: Die Nummer N kann inzwischen zu einem anderen Laufwerk gehören, der Pfad nicht.
/// Vor dem ersten Byte wird über dasselbe Handle geprüft, ob es wirklich noch der bestätigte Stick ist.
/// </summary>
internal static class RawDisk
{
    private const uint IoctlStorageGetDeviceNumber = 0x002D1080;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlDiskGetLengthInfo = 0x0007405C;
    private const uint IoctlDiskUpdateProperties = 0x00070140;

    /// <summary>Das erste MiB enthält die Partitionstabelle: Es wird zuletzt geschrieben (siehe <see cref="WriteImageAsync"/>).</summary>
    internal const int HeadBytes = 1024 * 1024;

    private const int SectorBytes = 4096;

    /// <summary>Öffnet den Stick zum Schreiben und prüft über das Handle Nummer, Größe, USB und Wechselmedium.</summary>
    /// <exception cref="ElevatedOperationException">Das Handle gehört nicht (mehr) zum bestätigten Stick.</exception>
    [SupportedOSPlatform("windows")]
    public static SafeFileHandle OpenVerified(UsbDisk disk, bool allowFixed)
    {
        var handle = File.OpenHandle(disk.DevicePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.WriteThrough);
        try
        {
            if (Mismatch(ReadFacts(handle), disk, allowFixed))
            {
                throw new ElevatedOperationException(Texts.Create_DiskChanged);
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>Passt das geöffnete Gerät nicht genau zum bestätigten Stick? (reine Funktion, für Tests)</summary>
    internal static bool Mismatch(DiskFacts facts, UsbDisk disk, bool allowFixed) =>
        facts.Number != disk.Number
        || facts.Length != disk.SizeBytes
        || facts.BusType != UsbDiskFinder.BusTypeUsb
        || (!allowFixed && !facts.IsRemovable);

    /// <summary>
    /// Schreibt das Abbild ab Byte 0. Das erste MiB (Partitionstabelle) kommt zuletzt: So hängt Windows während des Schreibens
    /// keine halbfertige Partition ein, und ein Abbruch hinterlässt einen leeren statt eines kaputten Sticks.
    /// Jeder Block wird ganz gelesen (<c>ReadAtLeastAsync</c>), damit alle Schreibvorgänge auf ganzen Sektoren beginnen.
    /// </summary>
    /// <param name="write">Schreibt Daten an eine Position (in der App: <c>RandomAccess.WriteAsync</c> auf das Stick-Handle).</param>
    /// <param name="report">Bekommt den Fortschritt in ganzen Prozent.</param>
    internal static async Task WriteImageAsync(
        Stream input, long totalBytes, Func<ReadOnlyMemory<byte>, long, CancellationToken, ValueTask> write,
        Action<int> report, int bufferSize, CancellationToken cancellationToken)
    {
        if (bufferSize < HeadBytes || bufferSize % SectorBytes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }

        var buffer = new byte[bufferSize];
        byte[]? head = null;
        long offset = 0;
        var lastPercent = -1;
        int read;
        while ((read = await input.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken)) > 0)
        {
            // Nur das letzte Stück kann kürzer sein: mit Nullen auf ganze Sektoren auffüllen
            var length = (read + SectorBytes - 1) / SectorBytes * SectorBytes;
            Array.Clear(buffer, read, length - read);
            var start = 0;
            if (offset == 0)
            {
                head = buffer[..Math.Min(length, HeadBytes)];
                start = head.Length;
            }

            if (length > start)
            {
                await write(buffer.AsMemory(start, length - start), offset + start, cancellationToken);
            }

            offset += read;
            var percent = (int)(Math.Min(offset, totalBytes) * 100 / Math.Max(1, totalBytes));
            if (percent != lastPercent)
            {
                lastPercent = percent;
                report(percent);
            }
        }

        if (head is not null)
        {
            await write(head, 0, cancellationToken);
        }
    }

    /// <summary>Windows soll die neue Partitionstabelle sofort lesen (wie „Update-Disk“, aber über dasselbe Handle).</summary>
    [SupportedOSPlatform("windows")]
    public static void UpdateProperties(SafeFileHandle handle) => Control(handle, IoctlDiskUpdateProperties, [], 0, 0);

    [SupportedOSPlatform("windows")]
    private static DiskFacts ReadFacts(SafeFileHandle handle)
    {
        // STORAGE_DEVICE_NUMBER: DeviceType (4), DeviceNumber (4), PartitionNumber (4)
        var number = Control(handle, IoctlStorageGetDeviceNumber, [], 12, 12);
        // GET_LENGTH_INFORMATION: Length (8)
        var length = Control(handle, IoctlDiskGetLengthInfo, [], 8, 8);
        // STORAGE_PROPERTY_QUERY { PropertyId = StorageDeviceProperty (0), QueryType = PropertyStandardQuery (0), AdditionalParameters }
        // → STORAGE_DEVICE_DESCRIPTOR: RemovableMedia bei Byte 10, BusType bei Byte 28
        var descriptor = Control(handle, IoctlStorageQueryProperty, new byte[12], 64, 32);
        return ParseFacts(number, length, descriptor);
    }

    /// <summary>Liest die Antworten der drei Abfragen (reine Funktion, für Tests).</summary>
    internal static DiskFacts ParseFacts(ReadOnlySpan<byte> deviceNumber, ReadOnlySpan<byte> lengthInfo, ReadOnlySpan<byte> descriptor) => new(
        BinaryPrimitives.ReadInt32LittleEndian(deviceNumber[4..]),
        BinaryPrimitives.ReadInt64LittleEndian(lengthInfo),
        BinaryPrimitives.ReadInt32LittleEndian(descriptor[28..]),
        descriptor[10] != 0);

    [SupportedOSPlatform("windows")]
    /// <param name="minimumAnswer">So viele Bytes muss die Antwort mindestens haben, sonst stimmt etwas nicht.</param>
    private static byte[] Control(SafeFileHandle handle, uint code, byte[] input, int outputSize, int minimumAnswer)
    {
        var output = new byte[outputSize];
        if (!DeviceIoControl(handle, code, input, input.Length, output, output.Length, out var returned, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return returned >= minimumAnswer ? output : throw new InvalidDataException($"Zu kurze Antwort vom Laufwerk ({returned} Bytes).");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint controlCode, byte[] inBuffer, int inBufferSize,
        byte[] outBuffer, int outBufferSize, out int bytesReturned, IntPtr overlapped);
}
