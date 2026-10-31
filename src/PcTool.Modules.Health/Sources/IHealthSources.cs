using PcTool.Modules.Health.Data;

namespace PcTool.Modules.Health.Sources;

// Alle Hardwarezugriffe des Health-Moduls liegen hinter diesen Interfaces.
// So kann HealthModule in Tests mit festen Werten (Fakes) geprüft werden.

/// <summary>Physische Laufwerke (ohne Adminrechte lesbar).</summary>
public interface IDiskSource
{
    IReadOnlyList<DiskInfo> ReadDisks();
}

/// <summary>Detailwerte der Laufwerke.</summary>
public interface IDiskReliabilitySource
{
    /// <exception cref="UnauthorizedAccessException">Wenn dafür Adminrechte nötig sind.</exception>
    IReadOnlyList<DiskReliability> Read();
}

/// <summary>Partitionen mit freiem Speicherplatz.</summary>
public interface IVolumeSource
{
    IReadOnlyList<VolumeInfo> ReadVolumes();
}

/// <summary>Akkudaten. <c>null</c> = konnte nicht gelesen werden.</summary>
public interface IBatterySource
{
    Task<IReadOnlyList<BatteryInfo>?> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Letzter Neustart, ausstehender Neustart, Aktivierung.</summary>
public interface IWindowsStateSource
{
    WindowsState Read();
}

/// <summary>Suche nach Windows-Updates. <c>null</c> = Suche nicht möglich (z. B. offline).</summary>
public interface IUpdateSource
{
    Task<UpdateStatus?> CheckAsync(CancellationToken cancellationToken);
}
