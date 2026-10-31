namespace PcTool.Modules.Performance.Data;

/// <summary>
/// Eine Messung (jede Sekunde neu). Werte, die nicht gemessen werden konnten, sind <c>null</c> bzw. die Liste ist leer.
/// </summary>
public sealed record PerformanceSample(
    CpuSample Cpu,
    MemorySample? Memory,
    SystemCounts? Counts,
    IReadOnlyList<DiskSample> Disks,
    IReadOnlyList<NetworkSample> Networks,
    IReadOnlyList<GpuSample> Gpus,
    TimeSpan Uptime);

/// <param name="TotalPercent">Auslastung wie im Task-Manager (0–100).</param>
/// <param name="CorePercents">Auslastung je logischem Prozessor, in Windows-Reihenfolge.</param>
/// <param name="CurrentMhz">Aktuelle Taktfrequenz (mit Turbo bzw. Stromsparen).</param>
public sealed record CpuSample(double? TotalPercent, IReadOnlyList<double> CorePercents, double? CurrentMhz);

/// <param name="TotalBytes">Für Windows nutzbarer Arbeitsspeicher.</param>
/// <param name="AvailableBytes">Davon verfügbar, wie „Verfügbar“ im Task-Manager.</param>
/// <param name="CommittedBytes">Zugesicherter Speicher (RAM + Auslagerungsdatei), wie „Zugesichert“ im Task-Manager.</param>
public sealed record MemorySample(long TotalBytes, long AvailableBytes, long? CommittedBytes, long? CommitLimitBytes)
{
    public long UsedBytes => TotalBytes - AvailableBytes;

    public double UsedPercent => TotalBytes <= 0 ? 0 : 100.0 * UsedBytes / TotalBytes;
}

/// <summary>Anzahl laufender Prozesse, Threads und Handles (wie im Task-Manager unter „CPU“).</summary>
public sealed record SystemCounts(int Processes, int Threads, int Handles);

/// <summary>Ein physisches Laufwerk.</summary>
/// <param name="Number">Windows-Nummer des Laufwerks (wie „Datenträger 0“).</param>
/// <param name="Letters">Laufwerksbuchstaben darauf, z. B. ["C:", "D:"].</param>
/// <param name="ActivePercent">Aktive Zeit, wie im Task-Manager.</param>
public sealed record DiskSample(int Number, IReadOnlyList<string> Letters, double ActivePercent, double ReadBytesPerSecond, double WriteBytesPerSecond);

/// <summary>Ein Netzwerkadapter.</summary>
/// <param name="Name">Name in Windows, z. B. "WLAN" oder "Ethernet".</param>
/// <param name="Description">Gerätename, z. B. "Intel(R) Wi-Fi 6E AX211".</param>
/// <param name="IsWireless">WLAN statt Kabel.</param>
/// <param name="IsVirtual">Virtueller Adapter (z. B. Hyper-V, VPN), kein echtes Gerät.</param>
/// <param name="IpAddresses">Nur zur Anzeige, wird nie geloggt oder exportiert.</param>
public sealed record NetworkSample(
    string Id,
    string Name,
    string Description,
    bool IsConnected,
    bool IsWireless,
    bool IsVirtual,
    long? LinkSpeedBitsPerSecond,
    double SendBitsPerSecond,
    double ReceiveBitsPerSecond,
    IReadOnlyList<string> IpAddresses);

/// <summary>Eine Grafikkarte (GPU), erkannt an ihrer LUID (eindeutige Nummer seit dem Systemstart).</summary>
/// <param name="UtilizationPercent">Wie im Task-Manager: der am stärksten ausgelastete Engine-Typ (3D, Video, Kopieren …).</param>
public sealed record GpuSample(string Luid, double UtilizationPercent, long? DedicatedUsedBytes, long? SharedUsedBytes, double? TemperatureCelsius);
