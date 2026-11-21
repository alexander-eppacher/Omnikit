using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Elevation;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>
/// Erstellt einen bootfähigen USB-Stick aus einer ISO-Datei – wie Rufus. Läuft im Hilfsprozess mit Adminrechten,
/// nur nach Bestätigung durch den Nutzer. Parameter: Datenträgernummer, bestätigte Größe in Bytes, Pfad der ISO
/// – oder Pfad der von Microsoft geladenen Windows-Datei (.esd) und ihre SHA-1-Prüfsumme aus dem Katalog.
/// <list type="bullet">
/// <item>Windows-ISO: MBR, eine FAT32-Partition (höchstens 32 GB), alle Dateien kopieren; eine install.wim über 4 GB
/// wird mit DISM in Teile (.swm) zerlegt, weil FAT32 keine größeren Dateien kann.</item>
/// <item>Andere ISO (z. B. Linux): 1:1 auf den Stick schreiben („DD-Modus“).</item>
/// </list>
/// </summary>
public sealed class CreateStickOperation : IElevatedOperation
{
    public const string OperationId = "stick.create";

    private const long Fat32MaxFile = 4L * 1024 * 1024 * 1024 - 1;
    private const long MinStickForWindows = 7_000_000_000;
    private const int BufferSize = 4 * 1024 * 1024;

    private readonly ILogger<CreateStickOperation> _logger;

    public CreateStickOperation(ILogger<CreateStickOperation> logger)
    {
        _logger = logger;
    }

    public string Id => OperationId;

    /// <summary>Ein Windows-Stick dauert je nach Stick 10–40 Minuten.</summary>
    public TimeSpan Timeout => TimeSpan.FromHours(2);

    /// <summary>Parameter für den Hilfsprozess. <paramref name="sha1"/> nur bei einer Windows-Datei (.esd) von Microsoft.</summary>
    public static IReadOnlyList<string> Arguments(UsbDisk disk, string sourcePath, string? sha1 = null) =>
        sha1 is null
            ? [disk.Number.ToString(CultureInfo.InvariantCulture), disk.SizeBytes.ToString(CultureInfo.InvariantCulture), sourcePath]
            : [disk.Number.ToString(CultureInfo.InvariantCulture), disk.SizeBytes.ToString(CultureInfo.InvariantCulture), sourcePath, sha1];

    public async Task<string> RunAsync(IReadOnlyList<string> arguments, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ElevatedOperationException(Texts.Create_WindowsOnly);
        }

        var (diskNumber, confirmedSize, isoPath, sha1) = ParseArguments(arguments);
        var disk = UsbDiskFinder.Check(diskNumber, confirmedSize);
        if (sha1 is not null)
        {
            await CreateFromDownloadAsync(disk, isoPath, sha1, progress, cancellationToken);
            return JsonSerializer.Serialize(new { mode = "windows" });
        }

        if (new FileInfo(isoPath).Length > disk.SizeBytes)
        {
            throw new ElevatedOperationException(Texts.Create_StickTooSmall);
        }

        _logger.LogInformation("Stick wird erstellt: Datenträger {Number} ({Name}, {Size} Bytes)", disk.Number, disk.Name, disk.SizeBytes);
        progress.Report(Texts.Create_Mounting);
        var source = await MountAsync(isoPath, cancellationToken);
        try
        {
            if (IsWindowsSetup(source))
            {
                await CreateWindowsStickAsync(disk, source, progress, cancellationToken);
                return JsonSerializer.Serialize(new { mode = "windows" });
            }
        }
        finally
        {
            await PowerShellCommand.RunAsync($"Dismount-DiskImage -ImagePath {PowerShellCommand.Quote(isoPath)} | Out-Null", CancellationToken.None);
        }

        await WriteRawAsync(disk, isoPath, progress, cancellationToken);
        return JsonSerializer.Serialize(new { mode = "raw" });
    }

    /// <summary>
    /// Strenge Prüfung der Parameter: Zahlen, ein vorhandener absoluter Pfad zu einer .iso-Datei –
    /// oder zu einer .esd-Datei mit Prüfsumme (40 Hexadezimalzeichen).
    /// </summary>
    internal static (int DiskNumber, long ConfirmedSize, string Path, string? Sha1) ParseArguments(IReadOnlyList<string> arguments)
    {
        var isEsd = arguments.Count == 4;
        if (arguments.Count is not (3 or 4)
            || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > 99
            || !long.TryParse(arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size <= 0
            || !Path.IsPathFullyQualified(arguments[2])
            || !arguments[2].EndsWith(isEsd ? ".esd" : ".iso", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(arguments[2])
            || (isEsd && (arguments[3].Length != 40 || !arguments[3].All(char.IsAsciiHexDigit))))
        {
            throw new ElevatedOperationException(Texts.Create_BadArguments);
        }

        return (number, size, arguments[2], isEsd ? arguments[3].ToLowerInvariant() : null);
    }

    /// <summary>
    /// Windows von Microsoft: Prüfsumme hier noch einmal prüfen (die Datei lag in einem Ordner, in den auch andere Programme
    /// des Nutzers schreiben können), dann den Installationsordner holen (beim ersten Mal gebaut, danach gemerkt) und kopieren.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private async Task CreateFromDownloadAsync(UsbDisk disk, string esdPath, string sha1, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (disk.SizeBytes < MinStickForWindows)
        {
            throw new ElevatedOperationException(Texts.Create_StickTooSmall);
        }

        progress.Report(Texts.Create_Verifying);
        if (!string.Equals(await Download.WindowsDownloader.Sha1Async(esdPath, cancellationToken), sha1, StringComparison.OrdinalIgnoreCase))
        {
            throw new ElevatedOperationException(Texts.Create_HashMismatch);
        }

        var media = await WindowsMediaBuilder.GetOrBuildAsync(esdPath, sha1, progress, cancellationToken);
        await CreateWindowsStickAsync(disk, media, progress, cancellationToken);
    }

    /// <summary>Windows-Installationsmedium: Startdatei für UEFI und ein Windows-Abbild.</summary>
    internal static bool IsWindowsSetup(string root) =>
        File.Exists(Path.Combine(root, "setup.exe"))
        && (File.Exists(Path.Combine(root, "sources", "install.wim")) || File.Exists(Path.Combine(root, "sources", "install.esd")));

    [SupportedOSPlatform("windows")]
    private async Task CreateWindowsStickAsync(UsbDisk disk, string source, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var esd = Path.Combine(source, "sources", "install.esd");
        if (File.Exists(esd) && new FileInfo(esd).Length > Fat32MaxFile)
        {
            throw new ElevatedOperationException(Texts.Create_EsdTooLarge);
        }

        progress.Report(Texts.Create_Formatting);
        var target = await PrepareFat32Async(disk.Number, cancellationToken);

        var wim = Path.Combine(source, "sources", "install.wim");
        var splitWim = File.Exists(wim) && new FileInfo(wim).Length > Fat32MaxFile;
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(file => !(splitWim && string.Equals(file, wim, StringComparison.OrdinalIgnoreCase)))
            .Where(file => !string.Equals(Path.GetFileName(file), WindowsMediaBuilder.DoneMarker, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await CopyAsync(source, target, files, progress, cancellationToken);

        if (splitWim)
        {
            await Dism.RunAsync(
                ["/Split-Image", $"/ImageFile:{wim}", $"/SWMFile:{Path.Combine(target, "sources", "install.swm")}", "/FileSize:3800"],
                percent => progress.Report(string.Format(CultureInfo.CurrentCulture, Texts.Create_SplittingPercent, (int)percent)), cancellationToken);
        }

        _logger.LogInformation("Windows-Stick fertig auf {Target}", target);
    }

    /// <summary>Stick leeren, MBR, eine aktive FAT32-Partition (Windows formatiert FAT32 nur bis 32 GB).</summary>
    [SupportedOSPlatform("windows")]
    private static async Task<string> PrepareFat32Async(int diskNumber, CancellationToken cancellationToken)
    {
        var n = diskNumber.ToString(CultureInfo.InvariantCulture);
        var letter = await PowerShellCommand.RunAsync(
            $$"""
            Get-Disk -Number {{n}} | Clear-Disk -RemoveData -RemoveOEM -Confirm:$false -ErrorAction SilentlyContinue
            $disk = Get-Disk -Number {{n}}
            # MBR: Nur dort gibt es eine „aktive“ Partition. Clear-Disk lässt GPT-Sticks GPT, also umstellen.
            if ($disk.PartitionStyle -eq 'RAW') { Initialize-Disk -Number {{n}} -PartitionStyle MBR }
            elseif ($disk.PartitionStyle -ne 'MBR') { Set-Disk -Number {{n}} -PartitionStyle MBR }
            $size = [Math]::Min($disk.Size - 8MB, 32GB - 8MB)
            $partition = New-Partition -DiskNumber {{n}} -Size $size -IsActive -AssignDriveLetter
            Format-Volume -Partition $partition -FileSystem FAT32 -NewFileSystemLabel 'OMNIKIT' -Confirm:$false -Force | Out-Null
            (Get-Partition -DiskNumber {{n}} -PartitionNumber $partition.PartitionNumber).DriveLetter
            """, cancellationToken);
        return letter.Length == 1 && char.IsAsciiLetter(letter[0])
            ? letter + @":\"
            : throw new InvalidOperationException($"Unerwarteter Laufwerksbuchstabe: {letter}");
    }

    [SupportedOSPlatform("windows")]
    private static async Task<string> MountAsync(string isoPath, CancellationToken cancellationToken)
    {
        var letter = await PowerShellCommand.RunAsync(
            $"(Mount-DiskImage -ImagePath {PowerShellCommand.Quote(isoPath)} -StorageType ISO -PassThru | Get-Volume).DriveLetter",
            cancellationToken);
        return letter.Length == 1 && char.IsAsciiLetter(letter[0])
            ? letter + @":\"
            : throw new ElevatedOperationException(Texts.Create_MountFailed);
    }

    /// <summary>Kopiert alle Dateien und meldet den Fortschritt in ganzen Prozent.</summary>
    private static async Task CopyAsync(string source, string target, IReadOnlyList<string> files, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var total = Math.Max(1, files.Sum(file => new FileInfo(file).Length));
        long copied = 0;
        var lastPercent = -1;
        var buffer = new byte[BufferSize];

        foreach (var file in files)
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copied += read;
                var percent = (int)(copied * 100 / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(string.Format(CultureInfo.CurrentCulture, Texts.Create_Copying, percent));
                }
            }
        }
    }

    /// <summary>„DD-Modus“: die ISO Sektor für Sektor auf den Datenträger schreiben.</summary>
    [SupportedOSPlatform("windows")]
    private async Task WriteRawAsync(UsbDisk disk, string isoPath, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report(Texts.Create_Formatting);
        var n = disk.Number.ToString(CultureInfo.InvariantCulture);
        await PowerShellCommand.RunAsync($"Get-Disk -Number {n} | Clear-Disk -RemoveData -RemoveOEM -Confirm:$false -ErrorAction SilentlyContinue", cancellationToken);

        using (var target = File.OpenHandle($@"\\.\PhysicalDrive{n}", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.WriteThrough))
        await using (var input = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
        {
            var total = input.Length;
            var buffer = new byte[BufferSize];
            long offset = 0;
            var lastPercent = -1;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                // Datenträger erwarten ganze Sektoren: das letzte Stück mit Nullen auf 4096 Bytes auffüllen
                var length = (read + 4095) / 4096 * 4096;
                Array.Clear(buffer, read, length - read);
                await RandomAccess.WriteAsync(target, buffer.AsMemory(0, length), offset, cancellationToken);
                offset += read;
                var percent = (int)(offset * 100 / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(string.Format(CultureInfo.CurrentCulture, Texts.Create_Writing, percent));
                }
            }
        }

        // Windows soll den neuen Inhalt erkennen
        await PowerShellCommand.RunAsync($"Update-Disk -Number {n}", cancellationToken);
        _logger.LogInformation("ISO 1:1 auf Datenträger {Number} geschrieben", disk.Number);
    }
}
