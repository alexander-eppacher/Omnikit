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
public sealed record DiskInfo(string DeviceId, string Name, DiskMediaType MediaType, long? SizeBytes, DiskHealthStatus HealthStatus);
