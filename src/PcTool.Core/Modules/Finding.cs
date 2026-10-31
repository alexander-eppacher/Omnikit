namespace PcTool.Core.Modules;

/// <summary>
/// Ein einzelner Befund eines Moduls, z. B. "SSD hat noch ca. 85 % Lebensdauer".
/// </summary>
/// <param name="Title">Kurze Überschrift.</param>
/// <param name="Explanation">Verständliche Erklärung für Einsteiger.</param>
/// <param name="Severity">Schweregrad (Ampel).</param>
/// <param name="Category">Optionaler Bereich zum Gruppieren in der UI, z. B. "Laufwerke" oder "Akku".</param>
/// <param name="Details">Optionale Rohwerte für die Expertenansicht, z. B. "Firmware: 7L2QFXV7 · Anschluss: NVMe".</param>
/// <param name="IsExpertOnly">
/// Nur in der Expertenansicht zeigen. Solche Befunde dürfen höchstens <see cref="Severity.Info"/> sein,
/// sonst stünde in der einfachen Ansicht eine gelbe Gesamtampel ohne sichtbaren Grund.
/// </param>
public sealed record Finding(
    string Title,
    string Explanation,
    Severity Severity,
    string? Category = null,
    string? Details = null,
    bool IsExpertOnly = false);
