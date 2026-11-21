using Omnikit.Modules.StickBuilder.Creation;

using static Omnikit.Modules.StickBuilder.Tests.CreateStickTests;

namespace Omnikit.Modules.StickBuilder.Tests;

public class RawDiskTests
{
    private const int Buffer = 2 * RawDisk.HeadBytes;

    /// <summary>Liefert beim Lesen nur wenige Bytes auf einmal – wie es Dateien und Netzlaufwerke manchmal tun.</summary>
    private sealed class TrickleStream(byte[] data, int chunk) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(chunk, buffer.Length)], cancellationToken);
    }

    private static byte[] Image(int length)
    {
        var data = new byte[length];
        new Random(42).NextBytes(data);
        return data;
    }

    /// <summary>Schreibt in einen „Stick“ im Speicher und merkt sich die Reihenfolge der Schreibvorgänge.</summary>
    private static async Task<(byte[] Disk, List<long> Offsets)> WriteAsync(Stream input, long length)
    {
        var disk = new byte[(length + 4095) / 4096 * 4096 + Buffer];
        var offsets = new List<long>();
        await RawDisk.WriteImageAsync(
            input, length,
            (data, offset, _) =>
            {
                Assert.Equal(0, offset % 4096);     // immer auf ganzen Sektoren
                Assert.Equal(0, data.Length % 4096);
                data.Span.CopyTo(disk.AsSpan((int)offset));
                offsets.Add(offset);
                return ValueTask.CompletedTask;
            },
            _ => { }, Buffer, TestContext.Current.CancellationToken);
        return (disk, offsets);
    }

    [Fact]
    public async Task ShortReadsStillGiveAnExactCopy()
    {
        var image = Image((3 * Buffer) + 12_345);

        var (disk, _) = await WriteAsync(new TrickleStream(image, 1000), image.Length);

        Assert.Equal(image, disk[..image.Length]);
        Assert.All(disk[image.Length..], b => Assert.Equal(0, b));   // Rest des letzten Sektors mit Nullen
    }

    [Fact]
    public async Task PartitionTableIsWrittenLast()
    {
        var image = Image((2 * Buffer) + 100);

        var (_, offsets) = await WriteAsync(new MemoryStream(image), image.Length);

        Assert.Equal(0, offsets[^1]);
        Assert.DoesNotContain(0L, offsets[..^1]);
        Assert.Equal(RawDisk.HeadBytes, offsets[0]);
    }

    [Fact]
    public async Task TinyImagesWork()
    {
        var image = Image(5000);

        var (disk, offsets) = await WriteAsync(new MemoryStream(image), image.Length);

        Assert.Equal(image, disk[..image.Length]);
        Assert.Equal([0L], offsets);
    }

    [Fact]
    public void OpenedDeviceMustMatchTheConfirmedStick()
    {
        var disk = new UsbDisk(3, "SanDisk", 31_914_983_424, DevicePath, IsRemovable: true);
        var same = new DiskFacts(3, 31_914_983_424, 7, IsRemovable: true);

        Assert.False(RawDisk.Mismatch(same, disk, allowFixed: false));
        Assert.True(RawDisk.Mismatch(same with { Number = 0 }, disk, allowFixed: true));
        Assert.True(RawDisk.Mismatch(same with { Length = 32_000_000_000 }, disk, allowFixed: true));
        Assert.True(RawDisk.Mismatch(same with { BusType = 11 }, disk, allowFixed: true));
        Assert.True(RawDisk.Mismatch(same with { IsRemovable = false }, disk, allowFixed: false));
        Assert.False(RawDisk.Mismatch(same with { IsRemovable = false }, disk, allowFixed: true));
    }

    [Fact]
    public void DeviceAnswersAreRead()
    {
        byte[] number = [7, 0, 0, 0, 3, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF];   // FILE_DEVICE_DISK, Nummer 3, keine Partition
        var length = BitConverter.GetBytes(31_914_983_424L);
        var descriptor = new byte[64];
        descriptor[10] = 1;                                                   // RemovableMedia
        descriptor[28] = 7;                                                   // BusTypeUsb

        Assert.Equal(new DiskFacts(3, 31_914_983_424, 7, true), RawDisk.ParseFacts(number, length, descriptor));
    }
}

public class EraseScriptTests
{
    private static readonly UsbDisk Disk = new(3, "SanDisk", 31_914_983_424, DevicePath, IsRemovable: true);

    [Fact]
    public void DiskIsOnlyAddressedByItsDevicePath()
    {
        var script = EraseScript.Build(Disk, allowFixed: false) + CreateStickOperation.FormatFat32Script;

        Assert.Contains("Get-Disk -Path $env:OMNIKIT_DISK_PATH", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-Number", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-DiskNumber", script, StringComparison.Ordinal);
        Assert.DoesNotContain(DevicePath, script, StringComparison.Ordinal);    // Pfad nur über die Umgebung
        Assert.Equal(DevicePath, EraseScript.Environment(Disk)[EraseScript.DevicePathVariable]);
    }

    [Fact]
    public void EverythingIsCheckedBeforeClearDisk()
    {
        var script = EraseScript.Build(Disk, allowFixed: false);
        var clear = script.IndexOf("Clear-Disk", StringComparison.Ordinal);

        foreach (var check in new[] { "$d.Number -ne 3", "$d.Size -ne 31914983424", "['BusType'].Value -ne 7",
                     "$d.IsBoot -ne $false", "$d.IsSystem -ne $false", "$d.IsReadOnly -ne $false", "'Removable Media'" })
        {
            var at = script.IndexOf(check, StringComparison.Ordinal);
            Assert.True(at >= 0 && at < clear, check);
        }

        // Scheitert Clear-Disk, muss das Skript abbrechen (ErrorActionPreference = Stop), nicht still weiterlaufen
        Assert.DoesNotContain("SilentlyContinue", script[clear..script.IndexOf('\n', clear)], StringComparison.Ordinal);
        Assert.Contains($"exit {EraseScript.ExitNotErased}", script[clear..], StringComparison.Ordinal);
    }

    [Fact]
    public void RemovableCheckOnlyWithoutPermissionForHardDisks()
    {
        Assert.DoesNotContain("Removable Media", EraseScript.Build(Disk, allowFixed: true), StringComparison.Ordinal);
    }
}

public class SourceOnStickTests
{
    private const string StickVolume = @"\\?\Volume{0a1b2c3d-0000-0000-0000-100000000000}\";

    [Theory]
    [InlineData(@"\\?\Volume{0a1b2c3d-0000-0000-0000-100000000000}\ISOs\ubuntu.iso", true)]
    [InlineData(@"\\?\VOLUME{0A1B2C3D-0000-0000-0000-100000000000}\omnikit.exe", true)]    // Groß/klein egal
    [InlineData(@"\\?\Volume{ffffffff-0000-0000-0000-100000000000}\ISOs\ubuntu.iso", false)] // anderes Volume (PC)
    [InlineData(@"\\?\Volume{0a1b2c3d-0000-0000-0000-1000000000001}\x.iso", false)]           // nur ähnlicher Anfang
    public void FileOnTheStickIsRecognized(string finalPath, bool onStick)
    {
        Assert.Equal(onStick, UsbDiskFinder.IsOnVolume(finalPath, ["E:\\", StickVolume]));
    }

    [Fact]
    public void DriveLettersAloneAreNotUsed()
    {
        // Der aufgelöste Pfad hat nie einen Buchstaben; ein Vergleich darüber würde subst und Bereitstellungspunkte übersehen
        Assert.False(UsbDiskFinder.IsOnVolume(@"E:\ubuntu.iso", ["E:\\"]));
        Assert.True(UsbDiskFinder.IsOnVolume(StickVolume + "x.iso", [StickVolume.TrimEnd('\\')]));
    }
}

public class HelperConfirmationTests
{
    private static readonly UsbDisk Stick = new(3, "SanDisk Ultra", 31_914_983_424, DevicePath, IsRemovable: true);

    [Fact]
    public void ConfirmationNamesTheDiskTheHelperCheckedItself()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Downloads");
        var request = new CreateStickOperation.Request(3, Stick.SizeBytes, DevicePath, false, Path.Combine(folder, "ubuntu-24.04.iso"), null);

        var text = CreateStickOperation.ConfirmText(Stick, request);

        Assert.Contains(Stick.DisplayName, text, StringComparison.Ordinal);
        Assert.Contains("Datenträger 3", text, StringComparison.Ordinal);
        Assert.Contains("„ubuntu-24.04.iso“", text, StringComparison.Ordinal);
        Assert.DoesNotContain(folder, text, StringComparison.Ordinal);   // nur der Dateiname, kein Pfad
    }

    [Fact]
    public void HardDisksAndDownloadsAreNamedClearly()
    {
        var hardDisk = Stick with { Name = "WD Elements", IsRemovable = false };
        var download = new CreateStickOperation.Request(3, Stick.SizeBytes, DevicePath, true, @"C:\x\a.esd", new string('a', 40));

        var text = CreateStickOperation.ConfirmText(hardDisk, download);

        Assert.StartsWith(Omnikit.Modules.StickBuilder.Resources.StickTexts.Confirm_FixedWarning, text, StringComparison.Ordinal);
        Assert.Contains(Omnikit.Modules.StickBuilder.Resources.StickTexts.Helper_ConfirmWindows, text, StringComparison.Ordinal);
    }
}
