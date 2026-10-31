using Omnikit.Core.Elevation;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Sources;

namespace Omnikit.Modules.Health.Tests.Fakes;

/// <summary>
/// Alle Datenquellen in einer Klasse; jeder Test setzt nur die Werte, die er braucht.
/// </summary>
internal sealed class FakeSources :
    IDiskSource, IDiskReliabilitySource, IVolumeSource, IBatterySource,
    IWindowsStateSource, IUpdateSource, IElevationRunner
{
    public List<DiskInfo> Disks { get; set; } = [new DiskInfo("0", "Test-SSD", DiskMediaType.Ssd, 512_000_000_000, DiskHealthStatus.Healthy)];

    /// <summary>Was das Lesen der Details ohne Adminrechte liefert. <c>null</c> = Zugriff verweigert.</summary>
    public List<DiskReliability>? Reliability { get; set; } = [new DiskReliability("0", 46, 0, null, null, null)];

    public List<VolumeInfo> Volumes { get; set; } = [new VolumeInfo(@"C:\", 500L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024)];
    public List<BatteryInfo>? Batteries { get; set; } = [new BatteryInfo("Test", 45000, 42090, 179)];
    public WindowsState WindowsState { get; set; } = new(DateTimeOffset.Now.AddDays(-1), false, true);
    public UpdateStatus? Updates { get; set; } = new(0, 0);

    /// <summary>Was der Hilfsprozess mit Adminrechten zurückgibt.</summary>
    public ElevationResult ElevationResult { get; set; } = new(ElevationStatus.Cancelled);
    public int ElevationCalls { get; private set; }

    public IReadOnlyList<DiskInfo> ReadDisks() => Disks;

    IReadOnlyList<DiskReliability> IDiskReliabilitySource.Read() =>
        Reliability ?? throw new UnauthorizedAccessException();

    public IReadOnlyList<VolumeInfo> ReadVolumes() => Volumes;

    public Task<IReadOnlyList<BatteryInfo>?> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BatteryInfo>?>(Batteries);

    WindowsState IWindowsStateSource.Read() => WindowsState;

    public Task<UpdateStatus?> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(Updates);

    public Task<ElevationResult> RunAsync(string operationId, CancellationToken cancellationToken)
    {
        ElevationCalls++;
        return Task.FromResult(ElevationResult);
    }
}
