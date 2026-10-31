using Omnikit.Core.Modules;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Resources;
using static Omnikit.Modules.Health.Evaluation.TextFormat;

namespace Omnikit.Modules.Health.Evaluation;

/// <summary>
/// Bewertet den Akku: aktuelle Kapazität im Vergleich zur ursprünglichen (Design-)Kapazität.
/// </summary>
public static class BatteryEvaluator
{
    public const int CapacityWarningBelow = 80;
    public const int CapacityCriticalBelow = 50;

    public static IReadOnlyList<Finding> Evaluate(BatteryInfo battery)
    {
        var category = HealthTexts.Category_Battery;
        var findings = new List<Finding>();

        if (battery is { DesignCapacity: > 0 and long design, FullChargeCapacity: long full })
        {
            // Neue Akkus haben manchmal etwas mehr als die Design-Kapazität: dann 100 % anzeigen.
            var capacityPercent = Math.Min(Percent(full, design), 100);
            var severity = RateCapacity(capacityPercent);
            var text = severity switch
            {
                Severity.Critical => HealthTexts.Battery_Capacity_Critical,
                Severity.Warning => HealthTexts.Battery_Capacity_Warning,
                _ => HealthTexts.Battery_Capacity_Ok,
            };
            // Die mWh-Werte sagen Einsteigern wenig: Sie stehen nur in der Expertenansicht.
            var details = Details(Format(HealthTexts.Details_DesignCapacity, design), Format(HealthTexts.Details_FullCapacity, full));
            findings.Add(new Finding(HealthTexts.Battery_Capacity_Title, Format(text, capacityPercent), severity, category, details));
        }

        if (battery.CycleCount is int cycles and > 0)
        {
            findings.Add(new Finding(HealthTexts.Battery_Cycles_Title, Format(HealthTexts.Battery_Cycles_Text, cycles), Severity.Ok, category));
        }

        return findings;
    }

    public static Severity RateCapacity(int capacityPercent) => capacityPercent switch
    {
        < CapacityCriticalBelow => Severity.Critical,
        < CapacityWarningBelow => Severity.Warning,
        _ => Severity.Ok,
    };
}
