using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Omnikit.Core.Reports;

namespace Omnikit.Shared;

/// <summary>
/// Schnittstelle zwischen Agent und Server. Der Agent fragt immer an (Pull), der Server meldet sich nie beim PC.
/// Außer bei der Anmeldung schickt der Agent seinen Token im Header <c>Authorization: Bearer …</c>.
/// </summary>
public static class AgentApi
{
    public const string Register = "/api/agent/register";
    public const string Report = "/api/agent/report";
    public const string Tasks = "/api/agent/tasks";

    public static string TaskResult(int taskId) => $"/api/agent/tasks/{taskId}/result";

    /// <summary>Größter erlaubter Bericht. Ein normaler Bericht hat wenige Kilobyte.</summary>
    public const int MaxReportBytes = 1024 * 1024;

    /// <summary>JSON-Einstellungen für beide Seiten: Namen in camelCase, Aufzählungen als Text („Install“, „Warning“).</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
}

/// <summary>Einmal-Code aus der Verwaltung gegen einen Geräte-Token tauschen.</summary>
public sealed record RegisterRequest(string Code, string ComputerName);

/// <param name="Token">Geheimer Token; der Server speichert nur seinen Hash. Wird nur einmal übertragen.</param>
public sealed record RegisterResponse(int DeviceId, string Token);

/// <summary>Bericht eines Geräts: derselbe Inhalt wie der JSON-Export der App plus die installierten Programme.</summary>
/// <param name="Installed">Programme laut winget; <c>null</c> = konnte nicht gelesen werden.</param>
public sealed record ReportRequest(string ComputerName, SystemReport Report, IReadOnlyList<InstalledProgram>? Installed);

public sealed record InstalledProgram(string Id, string Version);

/// <summary>Was ein Agent ausführen darf. Feste Liste – der Server kann keine beliebigen Befehle schicken.</summary>
public enum AgentTaskKind
{
    /// <summary>Programm über winget installieren.</summary>
    Install,

    /// <summary>Programm über winget aktualisieren.</summary>
    Upgrade,
}

/// <summary>Eine freigegebene Aufgabe für den Agent.</summary>
public sealed record AgentTask(int Id, AgentTaskKind Kind, string PackageId);

/// <param name="Message">Kurzer Text für die Verwaltung, z. B. „fertig“ oder „keine Internetverbindung“.</param>
public sealed record TaskResultRequest(bool Success, int ExitCode, string Message);
