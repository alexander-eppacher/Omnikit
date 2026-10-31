namespace PcTool.Modules.Health.Data;

public enum DiskMediaType
{
    Unknown,
    Hdd,
    Ssd,
}

public enum DiskHealthStatus
{
    Unknown,
    Healthy,
    Warning,
    Unhealthy,
}

/// <summary>
/// Grunddaten eines physischen Laufwerks (lesbar ohne Adminrechte).
/// </summary>
/// <param name="DeviceId">Windows-Nummer des Laufwerks, z. B. "0".</param>
/// <param name="Name">Anzeigename, z. B. "Samsung SSD 980 PRO 1TB".</param>
/// <param name="BusType">Anschluss, z. B. "NVMe", "SATA" oder "USB" (nur Expertenansicht).</param>
/// <param name="FirmwareVersion">Firmware-Stand des Laufwerks (nur Expertenansicht).</param>
public sealed record DiskInfo(
    string DeviceId,
    string Name,
    DiskMediaType MediaType,
    long? SizeBytes,
    DiskHealthStatus HealthStatus,
    string? BusType = null,
    string? FirmwareVersion = null);
