namespace Omnikit.Modules.StickBuilder;

/// <summary>Windows-Edition, die das Setup ohne Rückfrage installiert.</summary>
public enum WindowsEdition
{
    Home,
    Pro,
    Education,
    ProEducation,
}

/// <summary>Was mit dem Datenträger passiert.</summary>
public enum DiskMode
{
    /// <summary>Das Setup fragt, wohin installiert wird (sicher, Standard).</summary>
    ChooseInSetup,

    /// <summary>Datenträger 0 wird komplett gelöscht und neu eingeteilt (UEFI/GPT). Nur mit ausdrücklicher Bestätigung.</summary>
    WipeFirstDisk,
}

/// <summary>Alle Einstellungen für eine automatische Windows-Installation.</summary>
public sealed record StickOptions
{
    /// <summary>Anzeigesprache, z. B. "de-DE". Muss zur Sprache des Windows-Sticks passen.</summary>
    public string UiLanguage { get; init; } = "de-DE";

    /// <summary>Format für Datum, Zahlen und Währung, z. B. "de-AT".</summary>
    public string UserLocale { get; init; } = "de-DE";

    /// <summary>Tastatur im Format von Windows, z. B. "0407:00000407" (Deutsch).</summary>
    public string InputLocale { get; init; } = "0407:00000407";

    /// <summary>Windows-Zeitzone, z. B. "W. Europe Standard Time".</summary>
    public string TimeZone { get; init; } = "W. Europe Standard Time";

    public WindowsEdition Edition { get; init; } = WindowsEdition.Pro;

    /// <summary>Computername; leer = Windows wählt einen zufälligen Namen.</summary>
    public string ComputerName { get; init; } = string.Empty;

    /// <summary>Name des lokalen Kontos (wird Administrator).</summary>
    public string AccountName { get; init; } = string.Empty;

    /// <summary>Passwort des Kontos; leer = ohne Passwort.</summary>
    public string Password { get; init; } = string.Empty;

    public DiskMode Disk { get; init; } = DiskMode.ChooseInSetup;

    /// <summary>WLAN, mit dem sich der PC nach der Installation verbindet; leer = Windows fragt im Setup selbst.</summary>
    public string WifiName { get; init; } = string.Empty;

    /// <summary>WLAN-Passwort (WPA2/WPA3-Personal); leer = offenes WLAN ohne Passwort.</summary>
    public string WifiPassword { get; init; } = string.Empty;

    /// <summary>winget-Paket-IDs, die beim ersten Anmelden installiert werden.</summary>
    public IReadOnlyList<string> PackageIds { get; init; } = [];

    /// <summary>Vorinstallierte Apps (Paketnamen), die entfernt werden, z. B. "Microsoft.BingNews".</summary>
    public IReadOnlyList<string> RemoveApps { get; init; } = [];

    /// <summary>Datenschutz-Grundeinstellungen: Diagnosedaten auf Minimum, Werbe-ID aus, keine Werbe-Apps.</summary>
    public bool PrivacyDefaults { get; init; } = true;

    /// <summary>Bedienung von Explorer und Taskleiste (gilt für alle neuen Benutzer).</summary>
    public DesktopSettings Desktop { get; init; } = new();

    /// <summary>Einstellungen für den ganzen PC.</summary>
    public MachineSettings Machine { get; init; } = new();
}

/// <summary>Explorer und Taskleiste. Gilt für jeden Benutzer, der sich nach der Installation zum ersten Mal anmeldet.</summary>
public sealed record DesktopSettings
{
    /// <summary>Dateiendungen anzeigen (z. B. „.exe“) – hilft, gefährliche Anhänge zu erkennen.</summary>
    public bool ShowFileExtensions { get; init; } = true;

    /// <summary>Startknopf und Symbole links wie bei Windows 10 statt in der Mitte.</summary>
    public bool TaskbarLeft { get; init; }

    /// <summary>Widgets (Wetter, Nachrichten) in der Taskleiste ausblenden.</summary>
    public bool HideWidgets { get; init; }

    /// <summary>Rechtsklick zeigt sofort alle Einträge wie bei Windows 10 statt „Weitere Optionen anzeigen“.</summary>
    public bool ClassicContextMenu { get; init; }

    /// <summary>Die Suche im Startmenü zeigt nur Ergebnisse vom PC, keine Bing-Websuche.</summary>
    public bool NoWebSearch { get; init; }
}

/// <summary>Einstellungen für den ganzen PC.</summary>
public sealed record MachineSettings
{
    /// <summary>
    /// Prüfung auf TPM 2.0, Secure Boot und 4 GB RAM überspringen, damit Windows 11 auch auf älteren PCs startet.
    /// Microsoft unterstützt solche Installationen nicht offiziell.
    /// </summary>
    public bool BypassRequirements { get; init; }

    /// <summary>Windows verschlüsselt das Laufwerk nicht von selbst (BitLocker-Geräteverschlüsselung).</summary>
    public bool PreventDeviceEncryption { get; init; }

    /// <summary>„Schnellstart“ aus: Herunterfahren fährt wirklich ganz herunter (hilft bei Treiber- und Dual-Boot-Problemen).</summary>
    public bool DisableFastStartup { get; init; }

    /// <summary>Passwörter lokaler Konten laufen nicht nach 42 Tagen ab.</summary>
    public bool PasswordNeverExpires { get; init; } = true;
}
