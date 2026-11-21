using System.Reflection;
using System.Xml.Linq;

namespace Omnikit.Modules.StickBuilder;

/// <summary>
/// Erzeugt die Antwortdatei <c>autounattend.xml</c> für eine automatische Windows-11-Installation.
/// Windows Setup liest sie vom Stammverzeichnis des USB-Sticks. Aufbau nach der Microsoft-Dokumentation
/// („Unattended Windows Setup Reference“); Vorbild: Christoph Schneegans' Unattend-Generator (MIT), kein Code übernommen.
/// </summary>
public static class AnswerFile
{
    public const string ScriptFolder = @"C:\Windows\Setup\Scripts";
    public const string FirstStartScriptName = "omnikit-erststart.ps1";
    public const string PreparationScriptName = "omnikit-vorbereitung.ps1";
    public const string WifiProfileName = "omnikit-wlan.xml";

    private static readonly XNamespace U = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace Wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
    private static readonly XNamespace Ext = "https://github.com/alexander-eppacher/Omnikit/stick-builder";

    // Gleiche Einstellungen für PCs mit Intel/AMD- und mit ARM-Prozessor; Setup nimmt die passende.
    private static readonly string[] Architectures = ["amd64", "arm64"];

    /// <summary>
    /// Packt die eingebetteten Skripte während der Installation aus der zwischengespeicherten Antwortdatei aus.
    /// Windows erlaubt höchstens 259 Zeichen je Befehl; kein „%“, weil Setup die Zeile über cmd ausführen kann.
    /// </summary>
    public const string ExtractCommand =
        "powershell.exe -NoProfile -Command \"$x=[xml](Get-Content C:\\Windows\\Panther\\unattend.xml -Raw -Encoding UTF8);" +
        "$x.unattend.Extensions.File|ForEach-Object{md (Split-Path $_.path) -Force|Out-Null;Set-Content $_.path $_.InnerText -Encoding UTF8}\"";

    /// <summary>Öffentliche generische Installationsschlüssel von Microsoft: wählen nur die Edition, aktivieren nichts.</summary>
    public static string GenericKey(WindowsEdition edition) => edition switch
    {
        WindowsEdition.Home => "YTMG3-N6DKC-DKB77-7M9GH-8HVX7",
        WindowsEdition.Pro => "VK7JG-NPHTM-C97JM-9MPGT-3V66T",
        WindowsEdition.Education => "YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY",
        WindowsEdition.ProEducation => "8PTT6-RNW4C-6V7J2-C2D3X-MHBPB",
        _ => throw new ArgumentOutOfRangeException(nameof(edition)),
    };

    /// <exception cref="ArgumentException">Die Optionen sind ungültig (vorher mit <see cref="StickOptionsValidator"/> prüfen).</exception>
    public static string Create(StickOptions options)
    {
        var errors = StickOptionsValidator.Validate(options);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        }

        var files = EmbeddedFiles(options);
        var root = new XElement(U + "unattend",
            new XAttribute(XNamespace.Xmlns + "wcm", Wcm),
            Pass("windowsPE", WindowsPe(options)),
            Pass("specialize", Specialize(options, files.Count > 0)),
            Pass("oobeSystem", OobeSystem(options)));

        if (files.Count > 0)
        {
            root.Add(new XElement(Ext + "Extensions",
                files.Select(file => new XElement(Ext + "File", new XAttribute("path", file.Path), file.Content))));
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var writer = new Utf8StringWriter();
        document.Save(writer, SaveOptions.None);
        return writer.ToString();
    }

    /// <summary>Das Erststart-Skript für die gewählten Programme (auch zum Nachlesen im ZIP).</summary>
    public static string FirstStartScript(IReadOnlyList<string> packageIds) =>
        LoadScript(FirstStartScriptName).Replace("__PACKAGES__", QuoteList(packageIds), StringComparison.Ordinal);

    /// <summary>Das Vorbereitungsskript (Apps entfernen, Datenschutz, Explorer, Taskleiste, System).</summary>
    public static string PreparationScript(StickOptions options) =>
        LoadScript(PreparationScriptName)
            .Replace("__REMOVEAPPS__", QuoteList(options.RemoveApps), StringComparison.Ordinal)
            .Replace("__PRIVACY__", Flag(options.PrivacyDefaults), StringComparison.Ordinal)
            .Replace("__FILEEXTENSIONS__", Flag(options.Desktop.ShowFileExtensions), StringComparison.Ordinal)
            .Replace("__TASKBARLEFT__", Flag(options.Desktop.TaskbarLeft), StringComparison.Ordinal)
            .Replace("__HIDEWIDGETS__", Flag(options.Desktop.HideWidgets), StringComparison.Ordinal)
            .Replace("__CLASSICCONTEXTMENU__", Flag(options.Desktop.ClassicContextMenu), StringComparison.Ordinal)
            .Replace("__NOWEBSEARCH__", Flag(options.Desktop.NoWebSearch), StringComparison.Ordinal)
            .Replace("__PREVENTENCRYPTION__", Flag(options.Machine.PreventDeviceEncryption), StringComparison.Ordinal)
            .Replace("__DISABLEFASTSTARTUP__", Flag(options.Machine.DisableFastStartup), StringComparison.Ordinal)
            .Replace("__PASSWORDNEVEREXPIRES__", Flag(options.Machine.PasswordNeverExpires), StringComparison.Ordinal);

    /// <summary>
    /// Befehle, die vor der Prüfung der Hardware laufen: Setup überspringt dann TPM 2.0, Secure Boot und RAM.
    /// Die Werte gelten nur im Setup selbst (Windows PE), nicht im installierten Windows.
    /// </summary>
    public static IReadOnlyList<string> BypassRequirementsCommands { get; } =
        new[] { "BypassTPMCheck", "BypassSecureBootCheck", "BypassRAMCheck" }
            .Select(name => $@"reg.exe add HKLM\SYSTEM\Setup\LabConfig /v {name} /t REG_DWORD /d 1 /f")
            .ToList();

    internal static IReadOnlyList<(string Path, string Content)> EmbeddedFiles(StickOptions options)
    {
        var files = new List<(string, string)>();
        if (NeedsPreparation(options))
        {
            files.Add(($@"{ScriptFolder}\{PreparationScriptName}", PreparationScript(options)));
        }

        if (options.PackageIds.Count > 0)
        {
            files.Add(($@"{ScriptFolder}\{FirstStartScriptName}", FirstStartScript(options.PackageIds)));
        }

        // Das Vorbereitungsskript trägt das WLAN ein und löscht die Datei danach wieder
        if (options.WifiName.Length > 0)
        {
            files.Add(($@"{ScriptFolder}\{WifiProfileName}", WifiProfile.Create(options.WifiName, options.WifiPassword)));
        }

        return files;
    }

    // Die Hardware-Prüfung fehlt hier: Sie läuft schon in Windows PE und braucht das Skript nicht.
    private static bool NeedsPreparation(StickOptions options)
    {
        var desktop = options.Desktop;
        var machine = options.Machine;
        return options.RemoveApps.Count > 0 || options.PrivacyDefaults || options.WifiName.Length > 0
            || desktop.ShowFileExtensions || desktop.TaskbarLeft || desktop.HideWidgets || desktop.ClassicContextMenu || desktop.NoWebSearch
            || machine.PreventDeviceEncryption || machine.DisableFastStartup || machine.PasswordNeverExpires;
    }

    private static IEnumerable<XElement> WindowsPe(StickOptions options)
    {
        foreach (var arch in Architectures)
        {
            yield return Component("Microsoft-Windows-International-Core-WinPE", arch,
                new XElement(U + "SetupUILanguage", new XElement(U + "UILanguage", options.UiLanguage)),
                new XElement(U + "InputLocale", options.InputLocale),
                new XElement(U + "SystemLocale", options.UserLocale),
                new XElement(U + "UILanguage", options.UiLanguage),
                new XElement(U + "UserLocale", options.UserLocale));

            yield return Component("Microsoft-Windows-Setup", arch,
                options.Machine.BypassRequirements ? RunSynchronous(BypassRequirementsCommands) : null,
                options.Disk == DiskMode.WipeFirstDisk ? WipeFirstDisk() : null,
                options.Disk == DiskMode.WipeFirstDisk
                    ? new XElement(U + "ImageInstall", new XElement(U + "OSImage", new XElement(U + "InstallTo",
                        new XElement(U + "DiskID", 0), new XElement(U + "PartitionID", 3))))
                    : null,
                new XElement(U + "UserData",
                    new XElement(U + "ProductKey",
                        new XElement(U + "Key", GenericKey(options.Edition)),
                        new XElement(U + "WillShowUI", "OnError")),
                    new XElement(U + "AcceptEula", "true")));
        }
    }

    /// <summary>UEFI/GPT: EFI-Systempartition, MSR, Rest für Windows. Löscht alles auf Datenträger 0.</summary>
    private static XElement WipeFirstDisk() =>
        new(U + "DiskConfiguration",
            new XElement(U + "Disk", Add(),
                new XElement(U + "DiskID", 0),
                new XElement(U + "WillWipeDisk", "true"),
                new XElement(U + "CreatePartitions",
                    CreatePartition(1, "EFI", size: 300),
                    CreatePartition(2, "MSR", size: 16),
                    CreatePartition(3, "Primary", size: null)),
                new XElement(U + "ModifyPartitions",
                    new XElement(U + "ModifyPartition", Add(),
                        new XElement(U + "Order", 1), new XElement(U + "PartitionID", 1),
                        new XElement(U + "Label", "System"), new XElement(U + "Format", "FAT32")),
                    new XElement(U + "ModifyPartition", Add(),
                        new XElement(U + "Order", 2), new XElement(U + "PartitionID", 3),
                        new XElement(U + "Label", "Windows"), new XElement(U + "Letter", "C"), new XElement(U + "Format", "NTFS")))));

    private static XElement CreatePartition(int order, string type, int? size) =>
        new(U + "CreatePartition", Add(),
            new XElement(U + "Order", order),
            new XElement(U + "Type", type),
            size is int megabytes ? new XElement(U + "Size", megabytes) : new XElement(U + "Extend", "true"));

    private static IEnumerable<XElement> Specialize(StickOptions options, bool hasFiles)
    {
        foreach (var arch in Architectures)
        {
            // Leerer Name: "*" = Windows wählt einen zufälligen Namen
            yield return Component("Microsoft-Windows-Shell-Setup", arch,
                new XElement(U + "ComputerName", options.ComputerName.Length > 0 ? options.ComputerName : "*"),
                new XElement(U + "TimeZone", options.TimeZone));

            if (!hasFiles)
            {
                continue;
            }

            var commands = new List<string> { ExtractCommand };
            if (NeedsPreparation(options))
            {
                commands.Add($@"powershell.exe -NoProfile -ExecutionPolicy Bypass -File {ScriptFolder}\{PreparationScriptName}");
            }

            yield return Component("Microsoft-Windows-Deployment", arch, RunSynchronous(commands));
        }
    }

    private static XElement RunSynchronous(IEnumerable<string> commands) =>
        new(U + "RunSynchronous",
            commands.Select((command, index) => new XElement(U + "RunSynchronousCommand", Add(),
                new XElement(U + "Order", index + 1),
                new XElement(U + "Path", command))));

    private static IEnumerable<XElement> OobeSystem(StickOptions options)
    {
        var installsPrograms = options.PackageIds.Count > 0;
        foreach (var arch in Architectures)
        {
            yield return Component("Microsoft-Windows-International-Core", arch,
                new XElement(U + "InputLocale", options.InputLocale),
                new XElement(U + "SystemLocale", options.UserLocale),
                new XElement(U + "UILanguage", options.UiLanguage),
                new XElement(U + "UserLocale", options.UserLocale));

            yield return Component("Microsoft-Windows-Shell-Setup", arch,
                new XElement(U + "OOBE",
                    new XElement(U + "HideEULAPage", "true"),
                    new XElement(U + "HideOEMRegistrationScreen", "true"),
                    // Lokales Konto statt Microsoft-Konto: Die Konto-Seiten entfallen, weil das Konto unten angelegt wird
                    new XElement(U + "HideOnlineAccountScreens", "true"),
                    // Ohne vorab eingetragenes WLAN bleibt die WLAN-Seite, damit der PC für die Programme ins Internet kommt
                    new XElement(U + "HideWirelessSetupInOOBE", options.WifiName.Length > 0 ? "true" : "false"),
                    // 3 = keine „empfohlenen“ Einstellungen (weniger Daten an Microsoft), 1 = empfohlene
                    new XElement(U + "ProtectYourPC", options.PrivacyDefaults ? 3 : 1)),
                new XElement(U + "UserAccounts", new XElement(U + "LocalAccounts",
                    new XElement(U + "LocalAccount", Add(),
                        new XElement(U + "Name", options.AccountName),
                        new XElement(U + "DisplayName", options.AccountName),
                        // Englischer Gruppenname: Windows übersetzt ihn selbst (z. B. „Administratoren“)
                        new XElement(U + "Group", "Administrators"),
                        Password(options.Password)))),
                // Einmal automatisch anmelden, damit das Erststart-Skript ohne Eingabe startet
                installsPrograms
                    ? new XElement(U + "AutoLogon",
                        new XElement(U + "Enabled", "true"),
                        new XElement(U + "LogonCount", 1),
                        new XElement(U + "Username", options.AccountName),
                        Password(options.Password))
                    : null,
                // „start“: Das Skript läuft in einem eigenen Fenster auf dem Desktop, statt die Anmeldung zu blockieren
                installsPrograms
                    ? new XElement(U + "FirstLogonCommands", new XElement(U + "SynchronousCommand", Add(),
                        new XElement(U + "Order", 1),
                        new XElement(U + "CommandLine",
                            $@"cmd.exe /c start ""Omnikit"" powershell.exe -NoProfile -ExecutionPolicy Bypass -File {ScriptFolder}\{FirstStartScriptName}"),
                        new XElement(U + "Description", "Omnikit-Erststart")))
                    : null);
        }
    }

    private static XElement Password(string password) =>
        new(U + "Password",
            new XElement(U + "Value", UnattendPassword.Encode(password)),
            new XElement(U + "PlainText", "false"));

    private static XElement Pass(string name, IEnumerable<XElement> components) =>
        new(U + "settings", new XAttribute("pass", name), components);

    private static XElement Component(string name, string architecture, params object?[] content) =>
        new(U + "component",
            new XAttribute("name", name),
            new XAttribute("processorArchitecture", architecture),
            new XAttribute("publicKeyToken", "31bf3856ad364e35"),
            new XAttribute("language", "neutral"),
            new XAttribute("versionScope", "nonSxS"),
            content);

    private static XAttribute Add() => new(Wcm + "action", "add");

    private static string Flag(bool value) => value ? "$true" : "$false";

    /// <summary>IDs sind vorher geprüft (nur Buchstaben, Ziffern, . + - _), daher sicher in einfachen Anführungszeichen.</summary>
    private static string QuoteList(IEnumerable<string> values) => string.Join(", ", values.Select(value => $"'{value}'"));

    private static string LoadScript(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Omnikit.Modules.StickBuilder.Scripts.{name}")
            ?? throw new InvalidOperationException($"Skript {name} fehlt in der App.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>StringWriter meldet sonst UTF-16 in der XML-Kopfzeile; Windows Setup erwartet UTF-8.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public Utf8StringWriter()
            : base(System.Globalization.CultureInfo.InvariantCulture)
        {
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
