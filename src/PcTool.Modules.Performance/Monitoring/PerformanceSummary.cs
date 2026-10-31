using PcTool.Core.Modules;
using PcTool.Modules.Performance.Resources;
using static PcTool.Modules.Performance.Monitoring.Units;

namespace PcTool.Modules.Performance.Monitoring;

/// <summary>Zusammenfassung oben auf der Seite: Ampel und ein Satz für Einsteiger.</summary>
public sealed record PerformanceSummary(Severity Severity, string Text)
{
    /// <param name="hasEnoughData">Gibt es schon 30 Sekunden Messwerte? Vorher wird nicht bewertet.</param>
    /// <param name="busyDisks">Namen der Laufwerke, die seit 30 Sekunden fast durchgehend beschäftigt sind, z. B. "C:".</param>
    public static PerformanceSummary Build(bool hasEnoughData, bool cpuHigh, bool memoryHigh, IReadOnlyList<string> busyDisks)
    {
        var hints = new List<string>();
        if (cpuHigh)
        {
            hints.Add(PerformanceTexts.Hint_Cpu);
        }

        if (memoryHigh)
        {
            hints.Add(PerformanceTexts.Hint_Memory);
        }

        foreach (var disk in busyDisks)
        {
            hints.Add(Format(PerformanceTexts.Hint_Disk, disk));
        }

        if (hints.Count > 0)
        {
            return new PerformanceSummary(Severity.Info, string.Join(" ", hints));
        }

        return hasEnoughData
            ? new PerformanceSummary(Severity.Ok, PerformanceTexts.Summary_Ok)
            : new PerformanceSummary(Severity.Ok, PerformanceTexts.Summary_Collecting);
    }
}
