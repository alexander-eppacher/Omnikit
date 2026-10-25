namespace PcTool.Core.SystemInfo;

/// <summary>
/// Informationen über das Gerät. Wird einmal beim Start ermittelt und an alle Module übergeben.
/// <c>null</c> heißt immer: "nicht verfügbar". Es wird nie geraten.
/// </summary>
public sealed record SystemContext
{
    /// <summary>z. B. "Microsoft Windows 11 Pro".</summary>
    public string? OsName { get; init; }

    /// <summary>z. B. "24H2".</summary>
    public string? OsDisplayVersion { get; init; }

    /// <summary>z. B. "26100.2314".</summary>
    public string? OsBuild { get; init; }

    public string? Manufacturer { get; init; }
    public string? Model { get; init; }

    /// <summary>Personenbezogen möglich: nur anzeigen, nicht loggen oder speichern.</summary>
    public string? SerialNumber { get; init; }

    public VmType? VmType { get; init; }
    public bool? IsLaptop { get; init; }

    /// <summary>Ob die App gerade MIT Adminrechten läuft.</summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// Ob das Windows-Konto ein Administrator-Konto ist (auch wenn die App gerade ohne Adminrechte läuft).
    /// Bestimmt, ob Windows bei einer Admin-Abfrage nur "Ja/Nein" oder Benutzername und Passwort verlangt.
    /// </summary>
    public bool? IsAdminAccount { get; init; }
    public bool IsWindows { get; init; }

    /// <summary>Ja/Nein oder <c>null</c>, wenn sich das nicht feststellen ließ.</summary>
    public bool? IsVirtualMachine => VmType is null ? null : VmType != SystemInfo.VmType.None;
}
