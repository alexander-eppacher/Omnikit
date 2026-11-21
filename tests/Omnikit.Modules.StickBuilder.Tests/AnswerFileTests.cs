using System.IO.Compression;
using System.Xml.Linq;

namespace Omnikit.Modules.StickBuilder.Tests;

public class AnswerFileTests
{
    private static readonly XNamespace U = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Ext = "https://github.com/alexander-eppacher/Omnikit/stick-builder";

    private static readonly StickOptions Basic = new() { AccountName = "Anna" };

    // Keine Programme, keine Apps entfernen, keine Einstellungen
    private static readonly StickOptions Nothing = Basic with
    {
        PrivacyDefaults = false,
        RemoveApps = [],
        Desktop = new DesktopSettings { ShowFileExtensions = false },
        Machine = new MachineSettings { PasswordNeverExpires = false },
    };

    private static XDocument Create(StickOptions options) => XDocument.Parse(AnswerFile.Create(options));

    private static IEnumerable<XElement> Components(XDocument document, string pass, string name) =>
        document.Root!.Elements(U + "settings")
            .Where(settings => (string?)settings.Attribute("pass") == pass)
            .Elements(U + "component")
            .Where(component => (string?)component.Attribute("name") == name);

    [Fact]
    public void HasAllThreePassesForIntelAmdAndArm()
    {
        var document = Create(Basic);

        Assert.Equal(["windowsPE", "specialize", "oobeSystem"], document.Root!.Elements(U + "settings").Select(s => (string?)s.Attribute("pass")));
        Assert.Equal(["amd64", "arm64"], Components(document, "windowsPE", "Microsoft-Windows-Setup").Select(c => (string?)c.Attribute("processorArchitecture")));
    }

    [Fact]
    public void DeclaresUtf8()
    {
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", AnswerFile.Create(Basic), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WindowsEdition.Home, "YTMG3-N6DKC-DKB77-7M9GH-8HVX7")]
    [InlineData(WindowsEdition.Pro, "VK7JG-NPHTM-C97JM-9MPGT-3V66T")]
    [InlineData(WindowsEdition.Education, "YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY")]
    public void EditionIsChosenWithGenericKey(WindowsEdition edition, string key)
    {
        var setup = Components(Create(Basic with { Edition = edition }), "windowsPE", "Microsoft-Windows-Setup").First();

        Assert.Equal(key, setup.Descendants(U + "Key").Single().Value);
        Assert.Equal("true", setup.Descendants(U + "AcceptEula").Single().Value);
    }

    [Fact]
    public void ByDefaultTheDiskIsChosenInSetup()
    {
        var setup = Components(Create(Basic), "windowsPE", "Microsoft-Windows-Setup").First();

        Assert.Empty(setup.Descendants(U + "DiskConfiguration"));
        Assert.Empty(setup.Descendants(U + "InstallTo"));
    }

    [Fact]
    public void WipingDiskZeroCreatesGptLayoutAndInstallsToPartitionThree()
    {
        var setup = Components(Create(Basic with { Disk = DiskMode.WipeFirstDisk }), "windowsPE", "Microsoft-Windows-Setup").First();

        Assert.Equal("true", setup.Descendants(U + "WillWipeDisk").Single().Value);
        Assert.Equal(["EFI", "MSR", "Primary"], setup.Descendants(U + "CreatePartition").Select(p => p.Element(U + "Type")!.Value));
        Assert.Equal("3", setup.Descendants(U + "InstallTo").Single().Element(U + "PartitionID")!.Value);
    }

    [Fact]
    public void LocalAdminAccountWithoutOnlineAccountScreens()
    {
        var shell = Components(Create(Basic with { Password = "geheim" }), "oobeSystem", "Microsoft-Windows-Shell-Setup").First();

        var account = shell.Descendants(U + "LocalAccount").Single();
        Assert.Equal("Anna", account.Element(U + "Name")!.Value);
        Assert.Equal("Administrators", account.Element(U + "Group")!.Value);
        Assert.Equal("ZwBlAGgAZQBpAG0AUABhAHMAcwB3AG8AcgBkAA==", account.Descendants(U + "Value").Single().Value);
        Assert.Equal("true", shell.Descendants(U + "HideOnlineAccountScreens").Single().Value);
    }

    [Fact]
    public void WithoutProgramsThereIsNoAutoLogonAndNoFirstStartScript()
    {
        var document = Create(Nothing);

        Assert.Empty(document.Descendants(U + "AutoLogon"));
        Assert.Empty(document.Descendants(U + "FirstLogonCommands"));
        Assert.Empty(document.Descendants(Ext + "Extensions"));
        Assert.Empty(document.Descendants(U + "RunSynchronous"));
    }

    [Fact]
    public void ProgramsLeadToOneAutoLogonAndEmbeddedFirstStartScript()
    {
        var document = Create(Basic with { PackageIds = ["7zip.7zip", "VideoLAN.VLC"] });

        Assert.Equal("1", document.Descendants(U + "AutoLogon").First().Element(U + "LogonCount")!.Value);
        Assert.Contains(AnswerFile.FirstStartScriptName, document.Descendants(U + "CommandLine").First().Value, StringComparison.Ordinal);

        var file = document.Descendants(Ext + "File").Single(f => ((string)f.Attribute("path")!).EndsWith(AnswerFile.FirstStartScriptName, StringComparison.Ordinal));
        Assert.Equal(AnswerFile.FirstStartScript(["7zip.7zip", "VideoLAN.VLC"]), file.Value);
        Assert.Contains("$Packages = @('7zip.7zip', 'VideoLAN.VLC')", file.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void PreparationScriptRemovesChosenAppsAndRunsInSpecialize()
    {
        var document = Create(Basic with { RemoveApps = ["Microsoft.BingNews"], PrivacyDefaults = true });

        var commands = document.Descendants(U + "RunSynchronousCommand").Select(c => c.Element(U + "Path")!.Value).Distinct().ToList();
        Assert.Equal(AnswerFile.ExtractCommand, commands[0]);
        Assert.Contains(AnswerFile.PreparationScriptName, commands[1], StringComparison.Ordinal);

        var script = AnswerFile.PreparationScript(Basic with { RemoveApps = ["Microsoft.BingNews"], PrivacyDefaults = true });
        Assert.Contains("$RemoveApps = @('Microsoft.BingNews')", script, StringComparison.Ordinal);
        Assert.Contains("$PrivacyDefaults = $true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPlaceholderInThePreparationScriptIsFilled()
    {
        Assert.DoesNotContain("__", AnswerFile.PreparationScript(Basic), StringComparison.Ordinal);
        Assert.DoesNotContain("__", AnswerFile.FirstStartScript(["7zip.7zip"]), StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopAndMachineSettingsReachThePreparationScript()
    {
        var script = AnswerFile.PreparationScript(Nothing with
        {
            Desktop = new DesktopSettings { ShowFileExtensions = false, TaskbarLeft = true, ClassicContextMenu = true },
            Machine = new MachineSettings { PasswordNeverExpires = false, PreventDeviceEncryption = true },
        });

        Assert.Contains("$ShowFileExtensions = $false", script, StringComparison.Ordinal);
        Assert.Contains("$TaskbarLeft = $true", script, StringComparison.Ordinal);
        Assert.Contains("$ClassicContextMenu = $true", script, StringComparison.Ordinal);
        Assert.Contains("$HideWidgets = $false", script, StringComparison.Ordinal);
        Assert.Contains("$PreventDeviceEncryption = $true", script, StringComparison.Ordinal);
        Assert.Contains("$PasswordNeverExpires = $false", script, StringComparison.Ordinal);
    }

    [Fact]
    public void OneSettingIsEnoughToRunThePreparationScript()
    {
        var document = Create(Nothing with { Machine = new MachineSettings { PasswordNeverExpires = false, DisableFastStartup = true } });

        Assert.Contains(document.Descendants(U + "Path"), path => path.Value.Contains(AnswerFile.PreparationScriptName, StringComparison.Ordinal));
    }

    [Fact]
    public void ByDefaultSetupChecksTheHardware()
    {
        var setup = Components(Create(Basic), "windowsPE", "Microsoft-Windows-Setup").First();

        Assert.Empty(setup.Descendants(U + "RunSynchronous"));
    }

    [Fact]
    public void BypassingRequirementsRunsInWindowsPeOnly()
    {
        var document = Create(Nothing with { Machine = new MachineSettings { PasswordNeverExpires = false, BypassRequirements = true } });

        var setup = Components(document, "windowsPE", "Microsoft-Windows-Setup").First();
        var commands = setup.Descendants(U + "Path").Select(path => path.Value).ToList();
        Assert.Equal(AnswerFile.BypassRequirementsCommands, commands);
        Assert.All(commands, command => Assert.Contains(@"HKLM\SYSTEM\Setup\LabConfig", command, StringComparison.Ordinal));
        Assert.All(commands, command => Assert.True(command.Length <= 259));

        // Das allein braucht kein Skript im installierten Windows
        Assert.Empty(Components(document, "specialize", "Microsoft-Windows-Deployment"));
    }

    [Fact]
    public void WithoutWifiSetupAsksForTheNetwork()
    {
        var document = Create(Basic);

        Assert.Equal("false", document.Descendants(U + "HideWirelessSetupInOOBE").First().Value);
        Assert.DoesNotContain(document.Descendants(Ext + "File"), file => ((string)file.Attribute("path")!).EndsWith(AnswerFile.WifiProfileName, StringComparison.Ordinal));
    }

    [Fact]
    public void WifiIsEmbeddedAndTheSetupPageIsSkipped()
    {
        var document = Create(Nothing with { WifiName = "Zuhause", WifiPassword = "geheim123" });

        Assert.Equal("true", document.Descendants(U + "HideWirelessSetupInOOBE").First().Value);
        var file = document.Descendants(Ext + "File").Single(f => ((string)f.Attribute("path")!).EndsWith(AnswerFile.WifiProfileName, StringComparison.Ordinal));
        // XML liest Zeilenenden immer als \n, unter Windows erzeugt das Profil \r\n
        Assert.Equal(WifiProfile.Create("Zuhause", "geheim123").ReplaceLineEndings("\n"), file.Value);

        // Das Vorbereitungsskript trägt es ein, auch wenn sonst nichts gewählt ist
        Assert.Contains(document.Descendants(U + "Path"), path => path.Value.Contains(AnswerFile.PreparationScriptName, StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractCommandFitsWindowsLimitAndHasNoPercentSign()
    {
        Assert.True(AnswerFile.ExtractCommand.Length <= 259, $"{AnswerFile.ExtractCommand.Length} Zeichen");
        Assert.DoesNotContain('%', AnswerFile.ExtractCommand);
    }

    [Fact]
    public void SpecialCharactersAreEscaped()
    {
        var xml = AnswerFile.Create(Basic with { AccountName = "Max & Moritz", Password = "<\"'&>" });

        var document = XDocument.Parse(xml);
        Assert.Equal("Max & Moritz", document.Descendants(U + "LocalAccount").First().Element(U + "Name")!.Value);
        Assert.Contains("Max &amp; Moritz", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => AnswerFile.Create(Basic with { AccountName = "" }));
        Assert.Throws<ArgumentException>(() => AnswerFile.Create(Basic with { PackageIds = ["--force"] }));
    }
}

public class StickOptionsValidatorTests
{
    private static readonly StickOptions Valid = new() { AccountName = "Anna" };

    [Fact]
    public void DefaultsWithNameAreValid()
    {
        Assert.Empty(StickOptionsValidator.Validate(Valid));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Anna/Bert")]
    [InlineData("a@b")]
    [InlineData("Punkt.")]
    [InlineData("EinSehrLangerKontoname")]
    [InlineData("Administrator")]
    [InlineData("gast")]
    [InlineData("   ")]          // nur Leerzeichen
    [InlineData(" Anna")]        // Leerzeichen am Rand sieht man nicht
    [InlineData("Anna ")]
    public void BadAccountNamesAreRejected(string name)
    {
        Assert.Single(StickOptionsValidator.Validate(Valid with { AccountName = name }));
    }

    [Fact]
    public void AccountAndComputerMustHaveDifferentNames()
    {
        Assert.Single(StickOptionsValidator.Validate(Valid with { AccountName = "Schule", ComputerName = "SCHULE" }));
        Assert.Empty(StickOptionsValidator.Validate(Valid with { AccountName = "Anna Bert", ComputerName = "SCHULE" }));   // Leerzeichen in der Mitte gehen
    }

    [Theory]
    [InlineData("SCHUL-PC-01", true)]
    [InlineData("", true)]
    [InlineData("12345", false)]
    [InlineData("-PC", false)]
    [InlineData("Büro", false)]
    [InlineData("SIEBZEHN-ZEICHEN1", false)]
    public void ComputerNamesFollowWindowsRules(string name, bool valid)
    {
        Assert.Equal(valid, StickOptionsValidator.Validate(Valid with { ComputerName = name }).Count == 0);
    }

    [Theory]
    [InlineData("Zuhause", "geheim123", true)]
    [InlineData("Café WLAN", "", true)]
    [InlineData("Zuhause", "kurz", false)]
    [InlineData("Zuhause", "Passwört123", false)]
    [InlineData("ÄÖÜÄÖÜÄÖÜÄÖÜÄÖÜÄ", "geheim123", true)]
    [InlineData("ÄÖÜÄÖÜÄÖÜÄÖÜÄÖÜÄÖ", "geheim123", false)]
    [InlineData("", "geheim123", true)]
    public void WifiFollowsWpaRules(string name, string password, bool valid)
    {
        Assert.Equal(valid, StickOptionsValidator.Validate(Valid with { WifiName = name, WifiPassword = password }).Count == 0);
    }

    [Fact]
    public void UnknownRegionAndAppsAreRejected()
    {
        Assert.NotEmpty(StickOptionsValidator.Validate(Valid with { TimeZone = "Mars Time" }));
        Assert.NotEmpty(StickOptionsValidator.Validate(Valid with { RemoveApps = ["Microsoft.WindowsStore"] }));
    }
}

public class StickPackageTests
{
    [Fact]
    public async Task ZipContainsAnswerFileGuideAndScripts()
    {
        using var stream = new MemoryStream();
        await StickPackage.WriteZipAsync(stream, new StickOptions { AccountName = "Anna", PackageIds = ["7zip.7zip"] }, CancellationToken.None);
        stream.Position = 0;

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(
            ["autounattend.xml", "ANLEITUNG.txt", "Skripte/omnikit-vorbereitung.ps1", "Skripte/omnikit-erststart.ps1"],
            zip.Entries.Select(entry => entry.FullName));

        // Antwortdatei ohne BOM, Skripte mit BOM (für Windows PowerShell 5.1)
        Assert.Equal((byte)'<', await FirstByteAsync(zip.GetEntry("autounattend.xml")!));
        Assert.Equal(0xEF, await FirstByteAsync(zip.GetEntry("Skripte/omnikit-erststart.ps1")!));
    }

    [Fact]
    public async Task ZipDoesNotContainTheWifiProfileSeparately()
    {
        using var stream = new MemoryStream();
        await StickPackage.WriteZipAsync(stream, new StickOptions { AccountName = "Anna", WifiName = "Zuhause", WifiPassword = "geheim123" }, CancellationToken.None);
        stream.Position = 0;

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.DoesNotContain(zip.Entries, entry => entry.Name.EndsWith(".xml", StringComparison.Ordinal) && entry.Name != "autounattend.xml");
    }

    [Fact]
    public void GuideWarnsWhenDiskIsWiped()
    {
        Assert.DoesNotContain("LÖSCHT", StickPackage.Guide(new StickOptions()), StringComparison.Ordinal);
        Assert.Contains("LÖSCHT", StickPackage.Guide(new StickOptions { Disk = DiskMode.WipeFirstDisk }), StringComparison.Ordinal);
    }

    private static async Task<int> FirstByteAsync(ZipArchiveEntry entry)
    {
        await using var stream = await entry.OpenAsync(CancellationToken.None);
        return stream.ReadByte();
    }
}

public sealed class WindowsStickFinderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "omnikit-stick-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RecognizesWindowsStickAndItsLanguage()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sources"));
        File.WriteAllText(Path.Combine(_root, "sources", "install.esd"), "x");
        File.WriteAllLines(Path.Combine(_root, "sources", "lang.ini"), ["[Available UI Languages]", "de-DE = 3", "", "[Fallback Languages]", "de-DE = en-us"]);

        var stick = WindowsStickFinder.Inspect(_root, "ESD-USB");

        Assert.NotNull(stick);
        Assert.Equal(["de-DE"], stick.Languages);
        Assert.False(stick.HasAnswerFile);
    }

    [Fact]
    public void OtherSticksAreIgnored()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "foto.jpg"), "x");

        Assert.Null(WindowsStickFinder.Inspect(_root, "FOTOS"));
    }
}

public class WifiProfileTests
{
    private static readonly XNamespace W = "http://www.microsoft.com/networking/WLAN/profile/v1";

    [Fact]
    public void Wpa2PersonalWithPassphrase()
    {
        var profile = XDocument.Parse(WifiProfile.Create("Café", "geheim123"));

        Assert.Equal("Café", profile.Root!.Element(W + "name")!.Value);
        Assert.Equal("436166C3A9", profile.Descendants(W + "hex").Single().Value);
        Assert.Equal("WPA2PSK", profile.Descendants(W + "authentication").Single().Value);
        Assert.Equal("AES", profile.Descendants(W + "encryption").Single().Value);
        Assert.Equal("passPhrase", profile.Descendants(W + "keyType").Single().Value);
        Assert.Equal("geheim123", profile.Descendants(W + "keyMaterial").Single().Value);
        Assert.Equal("auto", profile.Root.Element(W + "connectionMode")!.Value);
    }

    [Fact]
    public void OpenNetworkHasNoKey()
    {
        var profile = XDocument.Parse(WifiProfile.Create("Gast", ""));

        Assert.Equal("open", profile.Descendants(W + "authentication").Single().Value);
        Assert.Empty(profile.Descendants(W + "sharedKey"));
    }

    [Fact]
    public void SixtyFourHexDigitsAreARawKey()
    {
        var key = new string('a', 64);

        Assert.True(WifiProfile.IsValidPassword(key));
        Assert.Equal("networkKey", XDocument.Parse(WifiProfile.Create("Netz", key)).Descendants(W + "keyType").Single().Value);
    }

    [Fact]
    public void SpecialCharactersAreEscaped()
    {
        var profile = WifiProfile.Create("A&B <WLAN>", "pass\"word&<>");

        Assert.Equal("pass\"word&<>", XDocument.Parse(profile).Descendants(W + "keyMaterial").Single().Value);
    }
}
