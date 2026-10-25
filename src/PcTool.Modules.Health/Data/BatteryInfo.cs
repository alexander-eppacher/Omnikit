namespace PcTool.Modules.Health.Data;

/// <summary>
/// Akkudaten aus dem Akku-Bericht von Windows. Kapazitäten in mWh.
/// </summary>
/// <param name="Name">Bezeichnung des Akkus laut Bericht (kann fehlen).</param>
public sealed record BatteryInfo(string? Name, long? DesignCapacity, long? FullChargeCapacity, int? CycleCount);
