using Omnikit.Modules.Performance.Data;

namespace Omnikit.Modules.Performance.Sources;

// Alle Hardwarezugriffe der Leistungsseite liegen hinter diesen Interfaces,
// damit die Logik in Tests mit festen Werten geprüft werden kann.

/// <summary>
/// Liefert jede Sekunde eine neue Messung. Zwischen <see cref="Open"/> und <see cref="Close"/> bleibt die Messung offen,
/// damit Werte wie „pro Sekunde“ aus zwei aufeinanderfolgenden Messungen berechnet werden können.
/// </summary>
public interface IPerformanceSource
{
    void Open();

    /// <summary>Werte seit dem letzten Aufruf. Der erste Aufruf nach <see cref="Open"/> kann noch Lücken haben.</summary>
    PerformanceSample Read();

    void Close();
}

/// <summary>Feste Hardwarewerte (Prozessorname, Kerne, Speicher, Laufwerksmodelle, Grafikkarten).</summary>
public interface IHardwareInventorySource
{
    HardwareInventory Read();
}
