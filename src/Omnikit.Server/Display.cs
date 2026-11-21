using System.Globalization;
using System.Text.Json;
using Omnikit.Core.Modules;
using Omnikit.Core.Reports;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Shared;

namespace Omnikit.Server;

/// <summary>Kleine Helfer, damit die Seiten Zeiten, Ampel und Zustände einheitlich anzeigen.</summary>
public static class Display
{
    /// <summary>Zeit in der Zeitzone des Servers (Variable TZ im Container), z. B. „02.11.2026 14:05“.</summary>
    public static string Time(DateTime? utc) =>
        utc is DateTime value ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) : "–";

    public static string Severity(Severity? severity) => severity switch
    {
        Core.Modules.Severity.Ok => ServerTexts.Severity_Ok,
        Core.Modules.Severity.Info => ServerTexts.Severity_Info,
        Core.Modules.Severity.Warning => ServerTexts.Severity_Warning,
        Core.Modules.Severity.Critical => ServerTexts.Severity_Critical,
        _ => ServerTexts.Severity_Unknown,
    };

    /// <summary>CSS-Klasse für den Ampelpunkt.</summary>
    public static string SeverityClass(Severity? severity) => "dot sev-" + (severity?.ToString().ToLowerInvariant() ?? "none");

    public static string State(TaskState state) => state switch
    {
        TaskState.AwaitingApproval => ServerTexts.State_AwaitingApproval,
        TaskState.Approved => ServerTexts.State_Approved,
        TaskState.Running => ServerTexts.State_Running,
        TaskState.Succeeded => ServerTexts.State_Succeeded,
        TaskState.Failed => ServerTexts.State_Failed,
        TaskState.Rejected => ServerTexts.State_Rejected,
        _ => state.ToString(),
    };

    public static string Kind(AgentTaskKind kind) => kind switch
    {
        AgentTaskKind.Upgrade => ServerTexts.Kind_Upgrade,
        AgentTaskKind.SelfUpdate => ServerTexts.Kind_SelfUpdate,
        _ => ServerTexts.Kind_Install,
    };

    /// <summary>Letzter Bericht eines Geräts; <c>null</c>, wenn es noch keinen gibt oder er nicht lesbar ist.</summary>
    public static SystemReport? Report(Device device)
    {
        if (device.LastReportJson is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SystemReport>(device.LastReportJson, ReportExporter.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<InstalledProgram>? Installed(Device device) =>
        device.InstalledJson is null ? null : JsonSerializer.Deserialize<List<InstalledProgram>>(device.InstalledJson, AgentApi.JsonOptions);

    /// <summary>Name eines Moduls im Bericht.</summary>
    public static string Module(string moduleId) => moduleId switch
    {
        "health" => ServerTexts.Module_Health,
        "drivers" => ServerTexts.Module_Drivers,
        "bios" => ServerTexts.Module_Bios,
        _ => moduleId,
    };
}
