using Omnikit.Modules.Apps.Winget;

namespace Omnikit.Modules.Apps.Tests.Winget;

public class UpgradeTableParserTests
{
    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));

    [Fact]
    public void ReadsRealOutputFromTestVm()
    {
        var upgrades = UpgradeTableParser.Parse(Read("upgrade-vm-en.txt"));

        Assert.Equal(3, upgrades.Count);
        Assert.Equal(
            new AvailableUpgrade("Microsoft Edge WebView2 Runtime", "Microsoft.EdgeWebView2Runtime", "100.0.1185.36", "156.0.4314.58", "winget"),
            upgrades[0]);
        Assert.Equal("Windows Terminal", upgrades[2].Name);
        Assert.Equal("1.25.2733.0", upgrades[2].AvailableVersion);
    }

    [Fact]
    public void GermanHeadersAndSpecialRowsWork()
    {
        var upgrades = UpgradeTableParser.Parse(Read("upgrade-de-tricky.txt"));

        // Nur die erste Tabelle zählt; Hinweissätze und die zweite Tabelle (Pakete mit Pin) nicht.
        Assert.Equal(["7zip.7zip", "Notepad++.Notepad++", "Microsoft.VCRedist.2015+.x…", "Example.Tool"], upgrades.Select(u => u.Id));
        Assert.Equal("Ein Programm mit Größe", upgrades[3].Name);
        Assert.Equal("< 2.0", upgrades[3].InstalledVersion);
        Assert.Equal("winget", upgrades[1].Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("No installed package found matching input criteria.")]
    [InlineData("Name Id\n----------------\nfoo bar")]
    public void UnexpectedOutputGivesEmptyList(string output)
    {
        Assert.Empty(UpgradeTableParser.Parse(output));
    }
}

public class ExportParserTests
{
    [Fact]
    public void ReadsRealExportFromTestVm()
    {
        var packages = ExportParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "export-vm.json")));

        Assert.Equal(5, packages.Count);
        Assert.Contains(new InstalledPackage("Brave.Brave", "156.1.98.63", "winget"), packages);
        Assert.Contains(packages, p => p.Id == "Microsoft.WindowsAppRuntime.1.8" && p.Version == "> 1.8.10");
    }

    [Theory]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("{}")]
    [InlineData("""{"Sources":[{"Packages":[{"PackageIdentifier":"--force","Version":"1"}]}]}""")]
    public void InvalidContentGivesEmptyList(string json)
    {
        Assert.Empty(ExportParser.Parse(json));
    }
}

public class PackageIdTests
{
    [Theory]
    [InlineData("Mozilla.Firefox", true)]
    [InlineData("Notepad++.Notepad++", true)]
    [InlineData("Adobe.Acrobat.Reader.64-bit", true)]
    [InlineData("7zip.7zip", true)]
    [InlineData("--force", false)]          // könnte als Option verstanden werden
    [InlineData("-h", false)]
    [InlineData("Foo Bar", false)]
    [InlineData("Foo;calc", false)]
    [InlineData("Foo&calc", false)]
    [InlineData("..\\evil", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlySafeIdsAreAccepted(string? id, bool expected)
    {
        Assert.Equal(expected, PackageId.IsValid(id));
    }

    [Fact]
    public void VeryLongIdsAreRejected()
    {
        Assert.False(PackageId.IsValid(new string('a', 200)));
    }
}

public class WingetExitCodesTests
{
    [Theory]
    [InlineData(0x00000000u, WingetOutcome.Success)]
    [InlineData(0x8A150014u, WingetOutcome.PackageNotFound)]
    [InlineData(0x8A15002Bu, WingetOutcome.NothingToDo)]
    [InlineData(0x8A150107u, WingetOutcome.NoNetwork)]
    [InlineData(0x80072EE7u, WingetOutcome.NoNetwork)]
    [InlineData(0x8A150101u, WingetOutcome.AppInUse)]
    [InlineData(0x8A150109u, WingetOutcome.SuccessRebootRequired)]
    [InlineData(0x8A15007Du, WingetOutcome.WrongElevation)]
    [InlineData(0x12345678u, WingetOutcome.Failed)]
    public void CodesAreClassified(uint code, WingetOutcome expected)
    {
        Assert.Equal(expected, WingetExitCodes.Classify(unchecked((int)code)));
    }

    [Fact]
    public void RebootAndNothingToDoCountAsSuccess()
    {
        Assert.True(WingetExitCodes.IsSuccess(WingetOutcome.SuccessRebootRequired));
        Assert.True(WingetExitCodes.IsSuccess(WingetOutcome.NothingToDo));
        Assert.False(WingetExitCodes.IsSuccess(WingetOutcome.NoNetwork));
    }

    [Fact]
    public void CodeIsFormattedLikeTheDocumentation()
    {
        Assert.Equal("0x8A150014", WingetExitCodes.Format(unchecked((int)0x8A150014u)));
    }

    [Fact]
    public void EveryOutcomeHasAText()
    {
        foreach (var outcome in Enum.GetValues<WingetOutcome>())
        {
            Assert.False(string.IsNullOrWhiteSpace(OutcomeText.For(outcome)));
        }
    }
}
