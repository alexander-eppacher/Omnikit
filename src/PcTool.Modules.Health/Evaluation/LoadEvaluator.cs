using PcTool.Core.Modules;
using PcTool.Modules.Health.Data;
using PcTool.Modules.Health.Resources;
using static PcTool.Modules.Health.Evaluation.TextFormat;

namespace PcTool.Modules.Health.Evaluation;

/// <summary>
/// Zeigt die momentane Auslastung. Das ist nur eine Momentaufnahme, deshalb höchstens ein Hinweis, nie eine Warnung.
/// </summary>
public static class LoadEvaluator
{
    public const int MemoryHighFromPercent = 90;

    /// <summary>Erst ab diesem Unterschied zwischen eingebaut und nutzbar lohnt sich der Hinweis (VMs reservieren oft nur wenige MB).</summary>
    public const long MemoryReservedNoteFromBytes = 512L * 1024 * 1024;

    public static IReadOnlyList<Finding> Evaluate(SystemLoad load)
    {
        var category = HealthTexts.Category_Load;
        var findings = new List<Finding>();

        if (load.CpuPercent is int cpu)
        {
            findings.Add(new Finding(HealthTexts.Cpu_Title, Format(HealthTexts.Cpu_Text, cpu), Severity.Ok, category));
        }

        if (load is { MemoryTotalBytes: > 0 and long total, MemoryFreeBytes: long free })
        {
            var used = total - free;
            var usedPercent = Percent(used, total);
            var isHigh = usedPercent >= MemoryHighFromPercent;
            var text = Format(isHigh ? HealthTexts.Memory_High : HealthTexts.Memory_Ok, usedPercent, Bytes(used), Bytes(total));

            // Der Task-Manager zeigt oben den eingebauten Speicher. Damit niemand einen Fehler vermutet, erklären wir den Unterschied.
            if (load.MemoryInstalledBytes is long installed && installed - total >= MemoryReservedNoteFromBytes)
            {
                text += " " + Format(HealthTexts.Memory_Reserved, Bytes(installed));
            }

            findings.Add(new Finding(HealthTexts.Memory_Title, text, isHigh ? Severity.Info : Severity.Ok, category));
        }

        return findings;
    }
}
