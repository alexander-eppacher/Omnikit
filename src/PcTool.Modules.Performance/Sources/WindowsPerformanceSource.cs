using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PcTool.Core.Windows;
using PcTool.Modules.Performance.Data;
using PcTool.Modules.Performance.Monitoring;

namespace PcTool.Modules.Performance.Sources;

/// <summary>
/// Misst jede Sekunde CPU, Arbeitsspeicher, Laufwerke, Netzwerk und Grafik – mit denselben Windows-Quellen wie der Task-Manager.
/// Nur lesen, keine Treiber, keine Adminrechte. Fehlt eine Quelle (z. B. GPU in einer VM), bleibt der Wert leer.
/// </summary>
public sealed class WindowsPerformanceSource : IPerformanceSource
{
    private readonly ILogger<WindowsPerformanceSource> _logger;
    private readonly Dictionary<string, (long Sent, long Received, long Timestamp)> _lastNetwork = [];
    private readonly HashSet<string> _knownInterfaceIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _visibleAdapterIds;
    private Counters? _counters;

    public WindowsPerformanceSource(ILogger<WindowsPerformanceSource> logger)
    {
        _logger = logger;
    }

    public void Open()
    {
        if (!OperatingSystem.IsWindows() || _counters is not null)
        {
            return;
        }

        _counters = Counters.Open();
        if (_counters is null)
        {
            _logger.LogWarning("Leistungsindikatoren konnten nicht geöffnet werden");
        }
    }

    public PerformanceSample Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new PerformanceSample(new CpuSample(null, [], null), null, null, [], [], [], TimeSpan.Zero);
        }

        var counters = _counters;
        if (counters is not null && !counters.Query.Collect())
        {
            _logger.LogDebug("Leistungsindikatoren lieferten keine Werte");
        }

        var (memory, counts) = NativeMemory.Read();
        return new PerformanceSample(
            ReadCpu(counters),
            memory,
            counts,
            ReadDisks(counters),
            ReadNetworks(),
            ReadGpus(counters),
            TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    public void Close()
    {
        if (OperatingSystem.IsWindows())
        {
            _counters?.Query.Dispose();
        }

        _counters = null;
        _lastNetwork.Clear();
        _knownInterfaceIds.Clear();
        _visibleAdapterIds = null;
    }

    [SupportedOSPlatform("windows")]
    private static CpuSample ReadCpu(Counters? counters)
    {
        if (counters is null)
        {
            return new CpuSample(null, [], null);
        }

        var total = PdhQuery.GetValue(counters.CpuTotal);
        var cores = CounterInstances.OrderCores(PdhQuery.GetValues(counters.CpuCores)).Select(Clamp).ToList();

        // Aktueller Takt wie im Task-Manager: Nenntakt × „% Processor Performance“ (über 100 % bei Turbo).
        var nominal = PdhQuery.GetValue(counters.CpuFrequency);
        var performance = PdhQuery.GetValue(counters.CpuPerformance);
        double? mhz = nominal is > 0 && performance is > 0 ? nominal * performance / 100 : null;

        return new CpuSample(total is double value ? Clamp(value) : null, cores, mhz);
    }

    [SupportedOSPlatform("windows")]
    private static List<DiskSample> ReadDisks(Counters? counters)
    {
        if (counters is null)
        {
            return [];
        }

        var idle = PdhQuery.GetValues(counters.DiskIdle).ToDictionary(pair => pair.Key, pair => pair.Value);
        var read = PdhQuery.GetValues(counters.DiskRead).ToDictionary(pair => pair.Key, pair => pair.Value);
        var write = PdhQuery.GetValues(counters.DiskWrite).ToDictionary(pair => pair.Key, pair => pair.Value);

        var disks = new List<DiskSample>();
        foreach (var (instance, idlePercent) in idle)
        {
            if (CounterInstances.ParseDisk(instance) is not var (number, letters))
            {
                continue;
            }

            // Aktive Zeit wie im Task-Manager: alles, was nicht Leerlauf ist.
            disks.Add(new DiskSample(
                number, letters, Clamp(100 - idlePercent), read.GetValueOrDefault(instance), write.GetValueOrDefault(instance)));
        }

        return disks.OrderBy(disk => disk.Number).ToList();
    }

    [SupportedOSPlatform("windows")]
    private static List<GpuSample> ReadGpus(Counters? counters)
    {
        if (counters is null)
        {
            return [];
        }

        var usage = CounterInstances.AggregateGpuUsage(PdhQuery.GetValues(counters.GpuEngine));
        var dedicated = SumPerLuid(PdhQuery.GetValues(counters.GpuDedicated));
        var shared = SumPerLuid(PdhQuery.GetValues(counters.GpuShared));

        return usage.Keys.Union(dedicated.Keys)
            .Order(StringComparer.Ordinal)
            .Select(luid => new GpuSample(
                luid,
                usage.GetValueOrDefault(luid),
                dedicated.TryGetValue(luid, out var d) ? (long)d : null,
                shared.TryGetValue(luid, out var s) ? (long)s : null,
                GpuTemperature.TryRead(luid)))
            .ToList();
    }

    private List<NetworkSample> ReadNetworks()
    {
        var now = Stopwatch.GetTimestamp();
        var result = new List<NetworkSample>();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        // Die Liste der sichtbaren Adapter nur neu laden, wenn eine unbekannte Schnittstelle auftaucht (z. B. USB-WLAN eingesteckt).
        var newInterfaces = interfaces.Count(adapter => _knownInterfaceIds.Add(adapter.Id));
        if (newInterfaces > 0)
        {
            _visibleAdapterIds = ReadVisibleAdapterIds();
        }

        foreach (var adapter in interfaces)
        {
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel
                || !NetworkAdapterFilter.IsRealAdapter(adapter.Id, _visibleAdapterIds))
            {
                continue;
            }

            try
            {
                var statistics = adapter.GetIPStatistics();
                var sent = statistics.BytesSent;
                var received = statistics.BytesReceived;

                // Bit pro Sekunde aus dem Unterschied zur letzten Messung.
                double sendBits = 0, receiveBits = 0;
                if (_lastNetwork.TryGetValue(adapter.Id, out var last))
                {
                    var seconds = Stopwatch.GetElapsedTime(last.Timestamp, now).TotalSeconds;
                    if (seconds > 0)
                    {
                        sendBits = Math.Max(0, sent - last.Sent) * 8 / seconds;
                        receiveBits = Math.Max(0, received - last.Received) * 8 / seconds;
                    }
                }

                _lastNetwork[adapter.Id] = (sent, received, now);

                result.Add(new NetworkSample(
                    adapter.Id,
                    adapter.Name,
                    adapter.Description,
                    adapter.OperationalStatus == OperationalStatus.Up,
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                    NetworkAdapterFilter.IsVirtual(adapter.Description),
                    adapter.Speed > 0 ? adapter.Speed : null,
                    sendBits,
                    receiveBits,
                    ReadAddresses(adapter)));
            }
            catch (NetworkInformationException ex)
            {
                // Adapter kann gerade verschwunden sein (z. B. USB-Stick abgezogen)
                _logger.LogDebug(ex, "Netzwerkadapter konnte nicht gelesen werden");
            }
        }

        return result;
    }

    /// <summary>
    /// GUIDs der Adapter, die Windows anzeigt. MSFT_NetAdapter liefert versteckte Adapter (WAN-Miniports, Kernel-Debugger,
    /// Filter-Schichten) ohne Zusatzoption nicht. Bei einem Fehler null: dann wird nicht gefiltert.
    /// </summary>
    private HashSet<string>? ReadVisibleAdapterIds()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return Wmi.Query(@"root\StandardCimv2", "SELECT DeviceID FROM MSFT_NetAdapter")
                .Select(row => row["DeviceID"] as string)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "Liste der Netzwerkadapter nicht verfügbar, zeige alle Schnittstellen");
            return null;
        }
    }

    /// <summary>IPv4- und öffentliche IPv6-Adressen. Datenschutz: nur zur Anzeige, nie loggen oder exportieren.</summary>
    private static List<string> ReadAddresses(NetworkInterface adapter) =>
        adapter.GetIPProperties().UnicastAddresses
            .Select(unicast => unicast.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                || (address.AddressFamily == AddressFamily.InterNetworkV6 && !address.IsIPv6LinkLocal))
            .Select(address => address.ToString())
            .ToList();

    private static Dictionary<string, double> SumPerLuid(IEnumerable<KeyValuePair<string, double>> instances) =>
        instances
            .Select(pair => (Luid: CounterInstances.ParseGpuLuid(pair.Key), pair.Value))
            .Where(pair => pair.Luid is not null)
            .GroupBy(pair => pair.Luid!)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));

    private static double Clamp(double percent) => Math.Clamp(percent, 0, 100);

    /// <summary>Die geöffnete Abfrage mit allen Indikatoren. Fehlende Indikatoren sind <c>null</c>.</summary>
    [SupportedOSPlatform("windows")]
    private sealed record Counters(
        PdhQuery Query,
        IntPtr? CpuTotal,
        IntPtr? CpuCores,
        IntPtr? CpuFrequency,
        IntPtr? CpuPerformance,
        IntPtr? DiskIdle,
        IntPtr? DiskRead,
        IntPtr? DiskWrite,
        IntPtr? GpuEngine,
        IntPtr? GpuDedicated,
        IntPtr? GpuShared)
    {
        public static Counters? Open()
        {
            var query = PdhQuery.TryOpen();
            if (query is null)
            {
                return null;
            }

            var counters = new Counters(
                query,
                query.TryAdd(@"\Processor Information(_Total)\% Processor Utility"),
                query.TryAdd(@"\Processor Information(*)\% Processor Utility"),
                query.TryAdd(@"\Processor Information(_Total)\Processor Frequency"),
                query.TryAdd(@"\Processor Information(_Total)\% Processor Performance"),
                query.TryAdd(@"\PhysicalDisk(*)\% Idle Time"),
                query.TryAdd(@"\PhysicalDisk(*)\Disk Read Bytes/sec"),
                query.TryAdd(@"\PhysicalDisk(*)\Disk Write Bytes/sec"),
                query.TryAdd(@"\GPU Engine(*)\Utilization Percentage"),
                query.TryAdd(@"\GPU Adapter Memory(*)\Dedicated Usage"),
                query.TryAdd(@"\GPU Adapter Memory(*)\Shared Usage"));

            // Erste Messung als Ausgangspunkt; Prozent- und „pro Sekunde“-Werte gibt es ab der zweiten.
            query.Collect();
            return counters;
        }
    }
}
