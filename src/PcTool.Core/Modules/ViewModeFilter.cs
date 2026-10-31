using PcTool.Core.Settings;

namespace PcTool.Core.Modules;

/// <summary>
/// Wählt aus, welche Befunde in welcher Ansicht sichtbar sind.
/// Der Bericht selbst bleibt vollständig (z. B. für den JSON-Export); gefiltert wird nur die Anzeige.
/// </summary>
public static class ViewModeFilter
{
    public static IReadOnlyList<Finding> Apply(IEnumerable<Finding> findings, ViewMode mode)
    {
        if (mode == ViewMode.Expert)
        {
            return findings.ToList();
        }

        // Einfache Ansicht: Expertenbefunde ausblenden, Rohwerte weglassen.
        return findings
            .Where(finding => !finding.IsExpertOnly)
            .Select(finding => finding with { Details = null })
            .ToList();
    }
}
