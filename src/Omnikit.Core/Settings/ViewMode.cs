namespace Omnikit.Core.Settings;

/// <summary>
/// Wie viel die App anzeigt. Beide Ansichten zeigen dieselben Messwerte und dieselbe Ampel;
/// die Expertenansicht zeigt zusätzlich Fachwerte und Rohdaten.
/// </summary>
public enum ViewMode
{
    /// <summary>Für Einsteiger: Ampel und verständliche Sätze, Unwichtiges ausgeblendet.</summary>
    Simple,

    /// <summary>Für Helpdesk und Profis: zusätzlich Rohwerte wie Firmware, Fehlerzähler oder Kapazität in mWh.</summary>
    Expert,
}
