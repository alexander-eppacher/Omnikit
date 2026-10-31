using System.Management;
using Microsoft.Extensions.Logging;
using PcTool.Core.Windows;
using PcTool.Modules.Health.Data;

namespace PcTool.Modules.Health.Sources;

/// <summary>
/// Liest Laufwerke über die Windows-Storage-API (MSFT_PhysicalDisk und MSFT_StorageReliabilityCounter).
/// Seriennummern der Laufwerke werden bewusst nicht abgefragt.
/// </summary>
public sealed class WindowsDiskSource : IDiskSource, IDiskReliabilitySource
{
    private readonly ILogger<WindowsDiskSource> _logger;

    public WindowsDiskSource(ILogger<WindowsDiskSource> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DiskInfo> ReadDisks()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            return Wmi.Query(Wmi.StorageNamespace, "SELECT DeviceId, FriendlyName, MediaType, Size, HealthStatus, BusType, FirmwareVersion FROM MSFT_PhysicalDisk")
                .Select(row => new DiskInfo(
                    row["DeviceId"] as string ?? "?",
                    (row["FriendlyName"] as string)?.Trim() ?? "?",
                    ToMediaType(Wmi.ToLong(row["MediaType"])),
                    Wmi.ToLong(row["Size"]),
                    ToHealthStatus(Wmi.ToLong(row["HealthStatus"])),
                    ToBusName(Wmi.ToLong(row["BusType"])),
                    (row["FirmwareVersion"] as string)?.Trim()))
                .ToList();
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "Laufwerke konnten nicht gelesen werden");
            return [];
        }
    }

    public IReadOnlyList<DiskReliability> Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var result = new List<DiskReliability>();

        try
        {
            using var searcher = new ManagementObjectSearcher(Wmi.StorageNamespace, "SELECT * FROM MSFT_PhysicalDisk");
            using var disks = searcher.Get();

            foreach (var disk in disks)
            {
                using (disk)
                {
                    var deviceId = disk["DeviceId"] as string ?? "?";

                    // Die Detailwerte hängen als "verwandtes" Objekt am Laufwerk (wie bei Get-StorageReliabilityCounter).
                    foreach (var counter in ((ManagementObject)disk).GetRelated("MSFT_StorageReliabilityCounter"))
                    {
                        using (counter)
                        {
                            result.Add(new DiskReliability(
                                deviceId,
                                (int?)Wmi.ToLong(counter["Temperature"]),
                                (int?)Wmi.ToLong(counter["Wear"]),
                                Wmi.ToLong(counter["PowerOnHours"]),
                                Wmi.ToLong(counter["ReadErrorsTotal"]),
                                Wmi.ToLong(counter["WriteErrorsTotal"]),
                                (int?)Wmi.ToLong(counter["TemperatureMax"])));
                        }
                    }
                }
            }
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new UnauthorizedAccessException("Für die Laufwerksdetails sind Adminrechte nötig.", ex);
        }

        return result;
    }

    private static DiskMediaType ToMediaType(long? value) => value switch
    {
        3 => DiskMediaType.Hdd,
        4 => DiskMediaType.Ssd,
        _ => DiskMediaType.Unknown,
    };

    /// <summary>Anschluss laut MSFT_PhysicalDisk.BusType. Seltene Werte (z. B. Fibre Channel) bleiben leer.</summary>
    public static string? ToBusName(long? value) => value switch
    {
        1 => "SCSI",
        3 => "ATA",
        7 => "USB",
        8 => "RAID",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        17 => "NVMe",
        _ => null,
    };

    private static DiskHealthStatus ToHealthStatus(long? value) => value switch
    {
        0 => DiskHealthStatus.Healthy,
        1 => DiskHealthStatus.Warning,
        2 => DiskHealthStatus.Unhealthy,
        _ => DiskHealthStatus.Unknown,
    };
}
