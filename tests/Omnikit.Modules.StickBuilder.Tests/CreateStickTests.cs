using Omnikit.Core.Elevation;
using Omnikit.Modules.StickBuilder.Creation;

namespace Omnikit.Modules.StickBuilder.Tests;

public sealed class CreateStickTests : IDisposable
{
    /// <summary>So sieht MSFT_Disk.Path bei einem USB-Stick aus.</summary>
    internal const string DevicePath = @"\\?\usbstor#disk&ven_sandisk&prod_ultra&rev_1.00#4c530001#{53f56307-b6bf-11d0-94f2-00a0c91efb8b}";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "omnikit-create-" + Guid.NewGuid().ToString("N"));

    public CreateStickTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Iso()
    {
        var path = Path.Combine(_folder, "Win11_Deutsch.iso");
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void ValidArgumentsAreParsed()
    {
        var iso = Iso();

        var request = CreateStickOperation.ParseArguments(["2", "31914983424", DevicePath, "1", iso]);

        Assert.Equal(2, request.DiskNumber);
        Assert.Equal(31914983424, request.ConfirmedSize);
        Assert.Equal(DevicePath, request.DevicePath);
        Assert.True(request.AllowFixed);
        Assert.Equal(iso, request.Path);
        Assert.Null(request.Sha1);
    }

    [Fact]
    public void ArgumentsRoundTrip()
    {
        var iso = Iso();
        var disk = new UsbDisk(3, "SanDisk", 31914983424, DevicePath, IsRemovable: true);

        var request = CreateStickOperation.ParseArguments(CreateStickOperation.Arguments(disk, allowFixed: false, iso));

        Assert.Equal(new CreateStickOperation.Request(3, 31914983424, DevicePath, false, iso, null), request);
    }

    [Theory]
    [InlineData(@"C:\Windows")]                                   // kein Gerätepfad
    [InlineData(@"\\.\PhysicalDrive0")]                           // Nummer statt Gerät
    [InlineData("\\\\?\\usb\nstor")]                            // Steuerzeichen
    [InlineData("")]
    public void BadDevicePathsAreRejected(string devicePath)
    {
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", devicePath, "0", Iso()]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("2")]
    public void AllowFixedMustBeZeroOrOne(string allowFixed)
    {
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", DevicePath, allowFixed, Iso()]));
    }

    [Theory]
    [InlineData("-1", "100")]        // negative Nummer
    [InlineData("2; rm", "100")]     // keine Zahl
    [InlineData("100", "100")]       // zu große Nummer
    [InlineData("2", "0")]           // Größe fehlt
    [InlineData("2", "1e9")]
    public void BadNumbersAreRejected(string number, string size)
    {
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments([number, size, DevicePath, "0", Iso()]));
    }

    [Fact]
    public void OnlyExistingAbsoluteIsoFilesAreAccepted()
    {
        var text = Path.Combine(_folder, "notiz.txt");
        File.WriteAllText(text, "x");

        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", DevicePath, "0", text]));
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", DevicePath, "0", "relativ.iso"]));
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", DevicePath, "0", Path.Combine(_folder, "fehlt.iso")]));
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", DevicePath, "0"]));
    }

    [Fact]
    public void WindowsSetupIsRecognized()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "sources"));
        Assert.False(CreateStickOperation.IsWindowsSetup(_folder));

        File.WriteAllText(Path.Combine(_folder, "setup.exe"), "x");
        File.WriteAllText(Path.Combine(_folder, "sources", "install.wim"), "x");
        Assert.True(CreateStickOperation.IsWindowsSetup(_folder));
    }

    private static Dictionary<string, object?> Row(
        long busType = 7, object? isBoot = null, object? isSystem = null, long size = 32_000_000_000L, string? mediaType = "Removable Media",
        object? isReadOnly = null, string? path = DevicePath) => new()
    {
        ["Number"] = 2, ["FriendlyName"] = "SanDisk Ultra", ["BusType"] = (ushort)busType,
        ["IsBoot"] = isBoot ?? false, ["IsSystem"] = isSystem ?? false, ["IsReadOnly"] = isReadOnly ?? false,
        ["Size"] = (ulong)size, ["MediaType"] = mediaType, ["Path"] = path,
    };

    [Fact]
    public void NormalUsbStickMayBeErased()
    {
        Assert.Null(UsbDiskFinder.Allowed(Row(), allowFixed: false));
    }

    [Theory]
    [InlineData(11L)]  // SATA
    [InlineData(17L)]  // NVMe
    public void OnlyUsbDisks(long busType)
    {
        Assert.NotNull(UsbDiskFinder.Allowed(Row(busType: busType), allowFixed: true));
    }

    [Fact]
    public void SystemAndBootDisksNeverEvenWithFixedAllowed()
    {
        Assert.NotNull(UsbDiskFinder.Allowed(Row(isBoot: true), allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(Row(isSystem: true), allowFixed: true));   // z. B. Windows To Go
    }

    [Fact]
    public void UnknownValuesCountAsForbidden()
    {
        // Liefert Windows einen Wert nicht, wird nie geraten (auch nicht zugunsten des Löschens)
        Assert.NotNull(UsbDiskFinder.Allowed(Row(isBoot: "unbekannt"), allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(new Dictionary<string, object?>(Row()) { ["IsBoot"] = null }, allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(new Dictionary<string, object?>(Row()) { ["IsSystem"] = null }, allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(new Dictionary<string, object?>(Row()) { ["Size"] = null }, allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(new Dictionary<string, object?>(Row()) { ["BusType"] = "USB" }, allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(Row(path: null), allowFixed: true));
        Assert.NotNull(UsbDiskFinder.Allowed(Row(mediaType: null), allowFixed: false));
    }

    [Fact]
    public void LargeDisksAreNeverErased()
    {
        Assert.NotNull(UsbDiskFinder.Allowed(Row(size: 1_000_000_000_000L), allowFixed: true));   // externe 1-TB-Platte
    }

    [Fact]
    public void UsbHardDisksOnlyWhenExplicitlyAllowed()
    {
        var ssd = Row(mediaType: "External hard disk media");

        Assert.NotNull(UsbDiskFinder.Allowed(ssd, allowFixed: false));
        Assert.Null(UsbDiskFinder.Allowed(ssd, allowFixed: true));
    }

    [Fact]
    public void WriteProtectedSticksAreRejected()
    {
        Assert.NotNull(UsbDiskFinder.Allowed(Row(isReadOnly: true), allowFixed: false));
    }
}

public class AnswerFileImportTests
{
    [Fact]
    public void OwnAnswerFilesAreAccepted()
    {
        Assert.True(WindowsStickFinder.IsAnswerFile(AnswerFile.Create(new StickOptions { AccountName = "Anna" })));
    }

    [Theory]
    [InlineData("kein xml")]
    [InlineData("<unattend />")]                                    // falscher Namensraum
    [InlineData("<html xmlns=\"urn:schemas-microsoft-com:unattend\" />")]
    public void OtherFilesAreRejected(string content)
    {
        Assert.False(WindowsStickFinder.IsAnswerFile(content));
    }
}
