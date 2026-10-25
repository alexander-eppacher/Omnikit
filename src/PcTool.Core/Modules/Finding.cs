namespace PcTool.Core.Modules;

/// <summary>
/// Ein einzelner Befund eines Moduls, z. B. "SSD hat noch ca. 85 % Lebensdauer".
/// </summary>
/// <param name="Title">Kurze Überschrift.</param>
/// <param name="Explanation">Verständliche Erklärung für Einsteiger.</param>
/// <param name="Severity">Schweregrad (Ampel).</param>
/// <param name="Category">Optionaler Bereich zum Gruppieren in der UI, z. B. "Laufwerke" oder "Akku".</param>
public sealed record Finding(string Title, string Explanation, Severity Severity, string? Category = null);
