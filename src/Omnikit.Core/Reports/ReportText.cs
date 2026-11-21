using System.Globalization;
using System.Text;
using Omnikit.Core.Modules;
using Omnikit.Core.Resources;
using Omnikit.Core.SystemInfo;

namespace Omnikit.Core.Reports;

/// <summary>
/// Der Bericht als kurzer, lesbarer Text – zum Einfügen in ein Helpdesk-Ticket oder eine E-Mail („Als Text kopieren“).
/// Gleicher Inhalt wie der JSON-Export, also ebenfalls ohne Seriennummer.
/// </summary>
public static class ReportText
{
    /// <param name="title">Name der Seite, z. B. „Gerätezustand“.</param>
    public static string Create(SystemReport report, string title)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Format(CultureInfo.CurrentCulture, CoreTexts.Text_Heading, title, report.CreatedAt));
        text.AppendLine(string.Format(CultureInfo.CurrentCulture, CoreTexts.Text_System, SystemLine(report.System)));

        foreach (var module in report.Modules)
        {
            text.AppendLine(string.Format(CultureInfo.CurrentCulture, CoreTexts.Text_Overall, SeverityWord(module.OverallSeverity)));
            foreach (var finding in module.Findings.OrderByDescending(finding => finding.Severity))
            {
                text.AppendLine();
                text.AppendLine(string.Format(CultureInfo.CurrentCulture, CoreTexts.Text_Finding, SeverityWord(finding.Severity), finding.Title));
                AppendIndented(text, finding.Explanation);
                AppendIndented(text, finding.Details);
            }
        }

        return text.ToString().TrimEnd();
    }

    public static string SeverityWord(Severity severity) => severity switch
    {
        Severity.Ok => CoreTexts.Severity_Ok,
        Severity.Info => CoreTexts.Severity_Info,
        Severity.Warning => CoreTexts.Severity_Warning,
        _ => CoreTexts.Severity_Critical,
    };

    private static string SystemLine(ReportSystemInfo system)
    {
        var parts = new List<string?>
        {
            Join(" ", system.OsName, system.OsDisplayVersion),
            system.OsBuild is { Length: > 0 } build ? string.Format(CultureInfo.CurrentCulture, CoreTexts.Text_Build, build) : null,
            Join(" ", system.Manufacturer, system.Model),
            system.VmType is { } vm && vm != VmType.None ? string.Format(CultureInfo.CurrentCulture, CoreTexts.Text_Vm, vm) : null,
        };
        return Join(" · ", [.. parts]) ?? "?";
    }

    private static string? Join(string separator, params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return present.Count == 0 ? null : string.Join(separator, present);
    }

    private static void AppendIndented(StringBuilder text, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var line in value.Split('\n'))
        {
            text.Append("    ").AppendLine(line.TrimEnd('\r'));
        }
    }
}
