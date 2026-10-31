namespace Omnikit.Modules.Performance.Data;

/// <summary>
/// Feste Werte, die sich während des Betriebs nicht ändern. Werden einmal gelesen, wenn die Seite zum ersten Mal geöffnet wird.
/// </summary>
public sealed record HardwareInventory(CpuInfo? Cpu, MemoryInfo? Memory, IReadOnlyList<DiskInventory> Disks, IReadOnlyList<GpuInfo> Gpus)
{
    public static HardwareInventory Empty { get; } = new(null, null, [], []);
}

/// <param name="BaseMhz">Basistakt laut Hersteller.</param>
/// <param name="VirtualizationEnabled">Virtualisierung im BIOS/UEFI eingeschaltet.</param>
/// <param name="HypervisorPresent">Ein Hypervisor läuft (z. B. Hyper-V); dann meldet Windows die Virtualisierung als belegt.</param>
public sealed record CpuInfo(
    string Name,
    int? BaseMhz,
    int Sockets,
    int? Cores,
    int? LogicalProcessors,
    bool? VirtualizationEnabled,
    bool? HypervisorPresent,
    long? L1CacheBytes,
    long? L2CacheBytes,
    long? L3CacheBytes);

/// <param name="InstalledBytes">Eingebauter Arbeitsspeicher (oft mehr als nutzbar, weil die Grafik einen Teil reserviert).</param>
/// <param name="SpeedMts">Geschwindigkeit in MT/s (Task-Manager zeigt sie als „MHz“).</param>
public sealed record MemoryInfo(long? InstalledBytes, int? SpeedMts, int? SlotsUsed, int? SlotsTotal);

/// <param name="Number">Windows-Nummer, passt zu <see cref="DiskSample.Number"/>.</param>
public sealed record DiskInventory(int Number, string Model, DiskKind Kind);

public enum DiskKind
{
    Unknown,
    Hdd,
    Ssd,
}

/// <param name="Luid">Passt zu <see cref="GpuSample.Luid"/>.</param>
public sealed record GpuInfo(string Luid, string Name, long? DedicatedMemoryBytes, long? SharedMemoryBytes);
