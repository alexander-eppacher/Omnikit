namespace Omnikit.Modules.Apps.Winget;

/// <summary>Was bei einer Installation oder Aktualisierung herausgekommen ist – für eine verständliche Meldung.</summary>
public enum WingetOutcome
{
    Success,

    /// <summary>Hat geklappt, Windows muss aber neu starten.</summary>
    SuccessRebootRequired,

    /// <summary>War schon installiert bzw. schon aktuell – nichts zu tun.</summary>
    NothingToDo,

    NoNetwork,
    PackageNotFound,
    AppInUse,
    RebootRequiredFirst,
    Cancelled,
    BlockedByPolicy,
    DiskFull,
    NotSupportedOnThisPc,

    /// <summary>winget lehnt die Aktion ab, weil Omnikit mit Adminrechten läuft (bei Programmen nur für einen Benutzer).</summary>
    WrongElevation,

    TimedOut,
    Failed,
}

/// <summary>
/// Ordnet die Rückgabecodes von winget ein. Quelle: offizielle Liste „returnCodes.md“ im winget-cli-Repository
/// (abgerufen am 31.10.2026). Unbekannte Codes gelten als <see cref="WingetOutcome.Failed"/> und landen mit Code im Log.
/// </summary>
public static class WingetExitCodes
{
    private static readonly Dictionary<uint, WingetOutcome> Known = new()
    {
        [0x8A150109] = WingetOutcome.SuccessRebootRequired,   // INSTALL_REBOOT_REQUIRED_TO_FINISH
        [0x8A15010B] = WingetOutcome.SuccessRebootRequired,   // INSTALL_REBOOT_INITIATED

        [0x8A150061] = WingetOutcome.NothingToDo,             // PACKAGE_ALREADY_INSTALLED
        [0x8A15002B] = WingetOutcome.NothingToDo,             // UPDATE_NOT_APPLICABLE
        [0x8A15004F] = WingetOutcome.NothingToDo,             // UPGRADE_VERSION_NOT_NEWER

        [0x8A150107] = WingetOutcome.NoNetwork,               // INSTALL_NO_NETWORK
        [0x8A150008] = WingetOutcome.NoNetwork,               // DOWNLOAD_FAILED
        [0x8A150086] = WingetOutcome.NoNetwork,               // INSTALLER_ZERO_BYTE_FILE
        [0x8A15002E] = WingetOutcome.NoNetwork,               // DOWNLOAD_SIZE_MISMATCH
        [0x8A15000F] = WingetOutcome.NoNetwork,               // SOURCE_DATA_MISSING (Paketliste nie geladen)
        [0x8A150045] = WingetOutcome.NoNetwork,               // SOURCE_OPEN_FAILED
        [0x8A15004B] = WingetOutcome.NoNetwork,               // FAILED_TO_OPEN_ALL_SOURCES
        [0x8A15006D] = WingetOutcome.NoNetwork,               // SERVICE_UNAVAILABLE
        [0x80072EE2] = WingetOutcome.NoNetwork,               // WinINet: Zeitüberschreitung
        [0x80072EE7] = WingetOutcome.NoNetwork,               // WinINet: Servername nicht auflösbar
        [0x80072EFD] = WingetOutcome.NoNetwork,               // WinINet: keine Verbindung zum Server
        [0x80072EFE] = WingetOutcome.NoNetwork,               // WinINet: Verbindung abgebrochen

        [0x8A150014] = WingetOutcome.PackageNotFound,         // NO_APPLICATIONS_FOUND
        [0x8A150017] = WingetOutcome.PackageNotFound,         // NO_MANIFEST_FOUND
        [0x8A150035] = WingetOutcome.PackageNotFound,         // NOT_ALL_PACKAGES_FOUND

        [0x8A150101] = WingetOutcome.AppInUse,                // INSTALL_PACKAGE_IN_USE
        [0x8A150103] = WingetOutcome.AppInUse,                // INSTALL_FILE_IN_USE
        [0x8A150111] = WingetOutcome.AppInUse,                // INSTALL_PACKAGE_IN_USE_BY_APPLICATION
        [0x8A150102] = WingetOutcome.AppInUse,                // INSTALL_INSTALL_IN_PROGRESS

        [0x8A15010A] = WingetOutcome.RebootRequiredFirst,     // INSTALL_REBOOT_REQUIRED_FOR_INSTALL

        [0x8A15010C] = WingetOutcome.Cancelled,               // INSTALL_CANCELLED_BY_USER
        [0x8A150005] = WingetOutcome.Cancelled,               // CTRL_SIGNAL_RECEIVED
        [0x8A15006A] = WingetOutcome.Cancelled,               // APPTERMINATION_RECEIVED
        [0x800704C7] = WingetOutcome.Cancelled,               // ERROR_CANCELLED (z. B. Windows-Abfrage mit „Nein“ beantwortet)

        [0x8A15003A] = WingetOutcome.BlockedByPolicy,         // BLOCKED_BY_POLICY
        [0x8A15010F] = WingetOutcome.BlockedByPolicy,         // INSTALL_BLOCKED_BY_POLICY
        [0x8A15001B] = WingetOutcome.BlockedByPolicy,         // MSSTORE_BLOCKED_BY_POLICY
        [0x8A15001C] = WingetOutcome.BlockedByPolicy,         // MSSTORE_APP_BLOCKED_BY_POLICY

        [0x8A150105] = WingetOutcome.DiskFull,                // INSTALL_DISK_FULL

        [0x8A150010] = WingetOutcome.NotSupportedOnThisPc,    // NO_APPLICABLE_INSTALLER
        [0x8A150113] = WingetOutcome.NotSupportedOnThisPc,    // INSTALL_SYSTEM_NOT_SUPPORTED

        [0x8A15007D] = WingetOutcome.WrongElevation,          // ADMIN_CONTEXT_ACTION_PROHIBITED
        [0x8A150056] = WingetOutcome.WrongElevation,          // INSTALLER_PROHIBITS_ELEVATION
    };

    public static WingetOutcome Classify(int exitCode) =>
        exitCode == 0 ? WingetOutcome.Success
            : Known.TryGetValue(unchecked((uint)exitCode), out var outcome) ? outcome
            : WingetOutcome.Failed;

    /// <summary>Gilt als geschafft (grün), auch wenn noch ein Neustart fehlt oder nichts zu tun war.</summary>
    public static bool IsSuccess(WingetOutcome outcome) =>
        outcome is WingetOutcome.Success or WingetOutcome.SuccessRebootRequired or WingetOutcome.NothingToDo;

    /// <summary>Fehlercode so, wie winget und die Dokumentation ihn schreiben, z. B. "0x8A150014".</summary>
    public static string Format(int exitCode) => $"0x{unchecked((uint)exitCode):X8}";
}
