namespace Omnikit.Modules.Bios.Data;

/// <summary>Wie der PC startet.</summary>
public enum FirmwareMode
{
    Unknown,

    /// <summary>Altes BIOS („Legacy“ bzw. CSM).</summary>
    Legacy,

    /// <summary>UEFI – Voraussetzung für Windows 11 und Secure Boot.</summary>
    Uefi,
}

/// <summary>BIOS/UEFI des PCs. Die Seriennummer wird bewusst nicht gelesen.</summary>
/// <param name="Manufacturer">Hersteller des BIOS, z. B. „LENOVO“ oder „American Megatrends Inc.“.</param>
/// <param name="Version">BIOS-Version, wie sie der Hersteller auf seiner Support-Seite nennt.</param>
/// <param name="ReleaseDate">Datum der BIOS-Version.</param>
/// <param name="Mode">Startmodus.</param>
/// <param name="SecureBootEnabled">Secure Boot an/aus; <c>null</c> = unbekannt (z. B. altes BIOS).</param>
/// <param name="BoardManufacturer">Hersteller der Hauptplatine (bei selbst gebauten PCs der eigentliche BIOS-Lieferant).</param>
/// <param name="BoardProduct">Modell der Hauptplatine.</param>
public sealed record FirmwareInfo(
    string? Manufacturer,
    string? Version,
    DateOnly? ReleaseDate,
    FirmwareMode Mode,
    bool? SecureBootEnabled,
    string? BoardManufacturer,
    string? BoardProduct);
