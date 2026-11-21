using Omnikit.Core.Elevation;
using Omnikit.Modules.StickBuilder.Creation;

namespace Omnikit.Modules.StickBuilder.Tests;

public sealed class CreateStickTests : IDisposable
{
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

        var (number, size, path, sha1) = CreateStickOperation.ParseArguments(["2", "31914983424", iso]);

        Assert.Equal(2, number);
        Assert.Equal(31914983424, size);
        Assert.Equal(iso, path);
        Assert.Null(sha1);
    }

    [Theory]
    [InlineData("-1", "100")]        // negative Nummer
    [InlineData("2; rm", "100")]     // keine Zahl
    [InlineData("100", "100")]       // zu große Nummer
    [InlineData("2", "0")]           // Größe fehlt
    [InlineData("2", "1e9")]
    public void BadNumbersAreRejected(string number, string size)
    {
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments([number, size, Iso()]));
    }

    [Fact]
    public void OnlyExistingAbsoluteIsoFilesAreAccepted()
    {
        var text = Path.Combine(_folder, "notiz.txt");
        File.WriteAllText(text, "x");

        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", text]));
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", "relativ.iso"]));
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100", Path.Combine(_folder, "fehlt.iso")]));
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["2", "100"]));
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

    [Theory]
    [InlineData(7L, false, false, 32_000_000_000L, true)]    // USB-Stick
    [InlineData(11L, false, false, 32_000_000_000L, false)]  // SATA
    [InlineData(17L, false, false, 32_000_000_000L, false)]  // NVMe
    [InlineData(7L, true, false, 32_000_000_000L, false)]    // Startlaufwerk
    [InlineData(7L, false, true, 32_000_000_000L, false)]    // Systemlaufwerk (z. B. Windows To Go)
    [InlineData(7L, false, false, 1_000_000_000_000L, false)] // externe 1-TB-Platte
    public void OnlyUsbSticksMayBeErased(long busType, bool isBoot, bool isSystem, long size, bool allowed)
    {
        var row = new Dictionary<string, object?>
        {
            ["Number"] = 2, ["FriendlyName"] = "SanDisk Ultra", ["BusType"] = (ushort)busType,
            ["IsBoot"] = isBoot, ["IsSystem"] = isSystem, ["Size"] = (ulong)size,
        };

        Assert.Equal(allowed, UsbDiskFinder.Allowed(row) is null);
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
