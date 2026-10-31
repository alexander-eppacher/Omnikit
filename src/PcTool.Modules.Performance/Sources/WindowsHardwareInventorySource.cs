using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PcTool.Core.Windows;
using PcTool.Modules.Performance.Data;

namespace PcTool.Modules.Performance.Sources;

/// <summary>
/// Liest feste Hardwarewerte über WMI und DXGI. Dauert etwas länger, wird deshalb nur einmal gelesen.
/// Jeder Teil wird einzeln abgesichert: Fehlt z. B. die Speicherinfo, gibt es trotzdem die Prozessorwerte.
/// </summary>
public sealed class WindowsHardwareInventorySource : IHardwareInventorySource
{
    private readonly ILogger<WindowsHardwareInventorySource> _logger;

    public WindowsHardwareInventorySource(ILogger<WindowsHardwareInventorySource> logger)
    {
        _logger = logger;
    }

    public HardwareInventory Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return HardwareInventory.Empty;
        }

        return new HardwareInventory(
            Safe("Prozessor", ReadCpu),
            Safe("Arbeitsspeicher", ReadMemory),
            Safe("Laufwerke", ReadDisks) ?? [],
            Safe("Grafikkarten", DxgiAdapters.Read) ?? []);
    }

    [SupportedOSPlatform("windows")]
    private static CpuInfo? ReadCpu()
    {
        var processors = Wmi.Query(
            "SELECT Name, MaxClockSpeed, NumberOfCores, NumberOfLogicalProcessors, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor");
        if (processors.Count == 0)
        {
            return null;
        }

        var first = processors[0];

        // Primärer Cache (Level 3 in WMI = L1), über alle Kerne zusammengezählt wie im Task-Manager.
        var l1Kb = Wmi.Query("SELECT Level, InstalledSize FROM Win32_CacheMemory")
            .Where(row => Wmi.ToLong(row["Level"]) == 3)
            .Sum(row => Wmi.ToLong(row["InstalledSize"]) ?? 0);
        var hypervisor = Wmi.Query("SELECT HypervisorPresent FROM Win32_ComputerSystem").FirstOrDefault()?["HypervisorPresent"] as bool?;

        return new CpuInfo(
            (first["Name"] as string)?.Trim() ?? "?",
            (int?)Wmi.ToLong(first["MaxClockSpeed"]),
            processors.Count,
            SumInt(processors, "NumberOfCores"),
            SumInt(processors, "NumberOfLogicalProcessors"),
            first["VirtualizationFirmwareEnabled"] as bool?,
            hypervisor,
            l1Kb > 0 ? l1Kb * 1024 : null,
            KbToBytes(processors.Sum(row => Wmi.ToLong(row["L2CacheSize"]) ?? 0)),
            KbToBytes(processors.Sum(row => Wmi.ToLong(row["L3CacheSize"]) ?? 0)));
    }

    [SupportedOSPlatform("windows")]
    private static MemoryInfo ReadMemory()
    {
        var modules = Wmi.Query("SELECT Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
        var slots = Wmi.Query("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray").Sum(row => Wmi.ToLong(row["MemoryDevices"]) ?? 0);

        // Eingestellte Geschwindigkeit bevorzugen; "Speed" ist das, was der Riegel maximal kann.
        var speed = modules
            .Select(row => Wmi.ToLong(row["ConfiguredClockSpeed"]) is > 0 and long configured ? configured : Wmi.ToLong(row["Speed"]))
            .Where(value => value > 0)
            .DefaultIfEmpty()
            .Max();

        return new MemoryInfo(
            NativeMemory.InstalledBytes(),
            speed > 0 ? (int?)speed : null,
            modules.Count > 0 ? modules.Count : null,
            slots > 0 ? (int?)slots : null);
    }

    [SupportedOSPlatform("windows")]
    private static List<DiskInventory> ReadDisks() =>
        Wmi.Query(Wmi.StorageNamespace, "SELECT DeviceId, FriendlyName, MediaType FROM MSFT_PhysicalDisk")
            .Select(row => (Number: int.TryParse(row["DeviceId"] as string, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1, row))
            .Where(disk => disk.Number >= 0)
            .Select(disk => new DiskInventory(
                disk.Number,
                (disk.row["FriendlyName"] as string)?.Trim() ?? "?",
                Wmi.ToLong(disk.row["MediaType"]) switch
                {
                    3 => DiskKind.Hdd,
                    4 => DiskKind.Ssd,
                    _ => DiskKind.Unknown,
                }))
            .ToList();

    private T? Safe<T>(string part, Func<T?> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex) || ex is MarshalDirectiveException or DllNotFoundException or EntryPointNotFoundException)
        {
            _logger.LogWarning(ex, "Hardwarewerte ({Part}) konnten nicht gelesen werden", part);
            return null;
        }
    }

    private static int? SumInt(List<Dictionary<string, object?>> rows, string property)
    {
        var values = rows.Select(row => Wmi.ToLong(row[property])).OfType<long>().ToList();
        return values.Count > 0 ? (int)values.Sum() : null;
    }

    private static long? KbToBytes(long kilobytes) => kilobytes > 0 ? kilobytes * 1024 : null;
}
