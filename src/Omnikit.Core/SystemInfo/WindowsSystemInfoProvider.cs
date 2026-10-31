using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Omnikit.Core.SystemInfo;

/// <summary>
/// Liest Systeminformationen unter Windows über WMI und die Registry.
/// Jede Abfrage ist einzeln abgesichert: schlägt eine fehl, fehlt nur dieser Wert.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemInfoProvider : ISystemInfoProvider
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private readonly ILogger<WindowsSystemInfoProvider> _logger;

    public WindowsSystemInfoProvider(ILogger<WindowsSystemInfoProvider> logger)
    {
        _logger = logger;
    }

    public RawSystemInfo Read()
    {
        var os = QueryFirst("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
        var computer = QueryFirst("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
        var bios = QueryFirst("SELECT SerialNumber FROM Win32_BIOS");
        var product = QueryFirst("SELECT Version FROM Win32_ComputerSystemProduct");

        return new RawSystemInfo
        {
            OsName = os?["Caption"] as string,
            OsDisplayVersion = ReadRegistryString(CurrentVersionKey, "DisplayVersion"),
            OsBuild = BuildOsBuild(os?["BuildNumber"] as string),
            Manufacturer = computer?["Manufacturer"] as string,
            Model = computer?["Model"] as string,
            ProductVersion = product?["Version"] as string,
            SerialNumber = bios?["SerialNumber"] as string,
            HasBattery = HasBattery(),
            IsAdmin = Environment.IsPrivilegedProcess,
            IsAdminAccount = AdminAccountDetector.IsAdminAccount(),
            IsWindows = true,
        };
    }

    /// <summary>Build-Nummer plus Update-Stand, z. B. "26100" + UBR 2314 = "26100.2314".</summary>
    private string? BuildOsBuild(string? buildNumber)
    {
        if (buildNumber is null)
        {
            return null;
        }

        return ReadRegistryValue(CurrentVersionKey, "UBR") is int updateRevision
            ? $"{buildNumber}.{updateRevision}"
            : buildNumber;
    }

    private bool? HasBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_Battery");
            using var results = searcher.Get();
            return results.Count > 0;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _logger.LogWarning(ex, "WMI-Abfrage Win32_Battery fehlgeschlagen");
            return null;
        }
    }

    /// <summary>
    /// Führt eine WMI-Abfrage aus und gibt das erste Ergebnis zurück (oder null bei Fehler).
    /// Die Werte werden sofort in ein Dictionary kopiert, damit keine WMI-Objekte offen bleiben.
    /// </summary>
    private Dictionary<string, object?>? QueryFirst(string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();

            foreach (var result in results)
            {
                using (result)
                {
                    return result.Properties
                        .Cast<PropertyData>()
                        .ToDictionary(property => property.Name, property => (object?)property.Value);
                }
            }

            _logger.LogWarning("WMI-Abfrage lieferte kein Ergebnis: {Query}", query);
            return null;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            _logger.LogWarning(ex, "WMI-Abfrage fehlgeschlagen: {Query}", query);
            return null;
        }
    }

    private string? ReadRegistryString(string keyPath, string valueName) =>
        ReadRegistryValue(keyPath, valueName) as string;

    private object? ReadRegistryValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue(valueName);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Registry-Wert {Key}\\{Value} konnte nicht gelesen werden", keyPath, valueName);
            return null;
        }
    }
}
