namespace Omnikit.Core.SystemInfo;

/// <summary>
/// Rohdaten, so wie sie das Betriebssystem liefert (noch nicht bereinigt).
/// <c>null</c> heißt: Wert konnte nicht gelesen werden.
/// </summary>
public sealed record RawSystemInfo
{
    public string? OsName { get; init; }
    public string? OsDisplayVersion { get; init; }
    public string? OsBuild { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }

    /// <summary>
    /// Win32_ComputerSystemProduct.Version. Bei Lenovo steht hier der Verkaufsname (z. B. "ThinkBook 14 G7 ARP"),
    /// während <see cref="Model"/> nur die Typnummer enthält (z. B. "21MV").
    /// </summary>
    public string? ProductVersion { get; init; }
    public string? SerialNumber { get; init; }
    public bool? HasBattery { get; init; }
    public bool IsAdmin { get; init; }
    public bool? IsAdminAccount { get; init; }
    public bool IsWindows { get; init; }
}
