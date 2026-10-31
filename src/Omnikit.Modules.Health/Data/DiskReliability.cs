namespace Omnikit.Modules.Health.Data;

/// <summary>
/// Detailwerte eines Laufwerks (MSFT_StorageReliabilityCounter). Braucht oft Adminrechte.
/// Jeder Wert kann fehlen (<c>null</c>), weil nicht jedes Laufwerk alles meldet.
/// </summary>
/// <param name="DeviceId">Gehört zu <see cref="DiskInfo.DeviceId"/>.</param>
/// <param name="WearPercent">Verschleiß in Prozent: 0 = neu, 100 = geschätzte Lebensdauer erreicht (kann darüber liegen).</param>
/// <param name="TemperatureMaxCelsius">
/// "TemperatureMax" laut Laufwerk. Je nach Hersteller der höchste gemessene Wert oder die Warnschwelle,
/// deshalb nur als Rohwert in der Expertenansicht und nicht bewertet.
/// </param>
public sealed record DiskReliability(
    string DeviceId,
    int? TemperatureCelsius,
    int? WearPercent,
    long? PowerOnHours,
    long? ReadErrorsTotal,
    long? WriteErrorsTotal,
    int? TemperatureMaxCelsius = null);
