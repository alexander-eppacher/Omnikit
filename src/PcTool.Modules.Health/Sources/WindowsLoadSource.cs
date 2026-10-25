using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PcTool.Modules.Health.Data;

namespace PcTool.Modules.Health.Sources;

/// <summary>
/// Liest die momentane Auslastung von CPU (Leistungsindikator) und Arbeitsspeicher (WMI), ohne Treiber und ohne Adminrechte.
/// </summary>
public sealed class WindowsLoadSource : ISystemLoadSource
{
    private readonly ILogger<WindowsLoadSource> _logger;

    public WindowsLoadSource(ILogger<WindowsLoadSource> logger)
    {
        _logger = logger;
    }

    public SystemLoad Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new SystemLoad(null, null, null);
        }

        try
        {
            // Wie im Task-Manager, über eine Sekunde gemessen. Läuft im Hintergrund, die Wartezeit stört also nicht.
            var cpu = ProcessorUtilityCounter.Measure(TimeSpan.FromSeconds(1)) ?? ReadLoadPercentage();

            // Werte kommen in KB
            var os = Wmi.Query("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem").FirstOrDefault();
            var totalKb = Wmi.ToLong(os?["TotalVisibleMemorySize"]);
            var freeKb = Wmi.ToLong(os?["FreePhysicalMemory"]);
            long? installedKb = GetPhysicallyInstalledSystemMemory(out var installed) ? (long)installed : null;

            return new SystemLoad(cpu, totalKb * 1024, freeKb * 1024, installedKb * 1024);
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "Auslastung konnte nicht gelesen werden");
            return new SystemLoad(null, null, null);
        }
    }

    /// <summary>Älterer, ungenauerer Wert; nur falls der Leistungsindikator fehlt. Bei mehreren Prozessor-Sockeln der Durchschnitt.</summary>
    [SupportedOSPlatform("windows")]
    private static int? ReadLoadPercentage()
    {
        var values = Wmi.Query("SELECT LoadPercentage FROM Win32_Processor")
            .Select(row => Wmi.ToLong(row["LoadPercentage"]))
            .OfType<long>()
            .ToList();
        return values.Count > 0 ? (int?)values.Average() : null;
    }

    // Windows-Funktion aus kernel32.dll: eingebauter Arbeitsspeicher in KB (laut Firmware).
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
}
