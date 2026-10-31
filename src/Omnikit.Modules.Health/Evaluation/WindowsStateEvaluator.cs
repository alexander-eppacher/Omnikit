using Omnikit.Core.Modules;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Resources;
using static Omnikit.Modules.Health.Evaluation.TextFormat;

namespace Omnikit.Modules.Health.Evaluation;

/// <summary>
/// Bewertet den Zustand von Windows: letzter Neustart, ausstehender Neustart, Aktivierung, Updates.
/// </summary>
public static class WindowsStateEvaluator
{
    public const int UptimeInfoAboveDays = 14;
    public const int UptimeWarningAboveDays = 30;

    /// <param name="now">Aktuelle Zeit; wird übergeben, damit Tests feste Zeitpunkte nutzen können.</param>
    public static IReadOnlyList<Finding> Evaluate(WindowsState state, DateTimeOffset now)
    {
        var category = HealthTexts.Category_Windows;
        var findings = new List<Finding> { EvaluateUptime(state.LastBootTime, now, category) };

        if (state.RebootPending == true)
        {
            findings.Add(new Finding(HealthTexts.RebootPending_Title, HealthTexts.RebootPending_Text, Severity.Info, category));
        }

        findings.Add(state.IsActivated switch
        {
            true => new Finding(HealthTexts.Activation_Title, HealthTexts.Activation_Ok, Severity.Ok, category),
            false => new Finding(HealthTexts.Activation_Title, HealthTexts.Activation_Warning, Severity.Warning, category),
            null => new Finding(HealthTexts.Activation_Title, HealthTexts.Activation_Unknown, Severity.Info, category),
        });

        return findings;
    }

    /// <param name="updates"><c>null</c>, wenn die Suche nicht möglich war.</param>
    public static Finding EvaluateUpdates(UpdateStatus? updates)
    {
        var category = HealthTexts.Category_Windows;

        return updates switch
        {
            null => new Finding(HealthTexts.Updates_Title, HealthTexts.Updates_Unknown, Severity.Info, category),
            { Total: 0 } => new Finding(HealthTexts.Updates_Title, HealthTexts.Updates_Ok, Severity.Ok, category),
            { Security: > 0 } => new Finding(
                HealthTexts.Updates_Title, Format(HealthTexts.Updates_Warning, updates.Total, updates.Security), Severity.Warning, category),
            _ => new Finding(HealthTexts.Updates_Title, Format(HealthTexts.Updates_Info, updates.Total), Severity.Info, category),
        };
    }

    public static Severity RateUptime(int days) => days switch
    {
        > UptimeWarningAboveDays => Severity.Warning,
        > UptimeInfoAboveDays => Severity.Info,
        _ => Severity.Ok,
    };

    private static Finding EvaluateUptime(DateTimeOffset? lastBoot, DateTimeOffset now, string category)
    {
        if (lastBoot is null)
        {
            return new Finding(HealthTexts.Uptime_Title, HealthTexts.Uptime_Unknown, Severity.Info, category);
        }

        var days = Math.Max(0, (int)(now - lastBoot.Value).TotalDays);
        var severity = RateUptime(days);
        var text = (severity, days) switch
        {
            (Severity.Warning, _) => HealthTexts.Uptime_Warning,
            (Severity.Info, _) => HealthTexts.Uptime_Info,
            (_, 0) => HealthTexts.Uptime_Today,
            (_, 1) => HealthTexts.Uptime_Yesterday,
            _ => HealthTexts.Uptime_Ok,
        };

        return new Finding(HealthTexts.Uptime_Title, Format(text, days), severity, category);
    }
}
