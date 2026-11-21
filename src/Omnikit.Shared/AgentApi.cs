using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Omnikit.Core.Reports;

namespace Omnikit.Shared;

/// <summary>
/// Schnittstelle zwischen Agent und Server. Der Agent fragt immer an (Pull), der Server meldet sich nie beim PC.
/// Außer bei der Anmeldung schickt der Agent seinen Token im Header <c>Authorization: Bearer …</c>.
/// <para>
/// Versionen: Innerhalb von <c>v1</c> kommen nur Ergänzungen dazu (neue optionale Felder, neue Aufgabentypen). Beide Seiten
/// ignorieren Felder, die sie nicht kennen, und unbekannte Aufgabentypen werden zu <see cref="AgentTaskKind.Unknown"/>.
/// Ein Bruch bekäme einen neuen Pfad (<c>v2</c>) neben dem alten. Reihenfolge beim Aktualisieren: erst der Server, dann die Agents.
/// </para>
/// </summary>
public static class AgentApi
{
    /// <summary>Version der Schnittstelle (Teil jedes Pfads).</summary>
    public const int Version = 1;

    private const string Prefix = "/api/agent/v1";

    public const string Register = Prefix + "/register";
    public const string Report = Prefix + "/report";
    public const string Tasks = Prefix + "/tasks";

    /// <summary>Muster für den Server; der Agent nutzt <see cref="TaskResult"/>.</summary>
    public const string TaskResultPattern = Prefix + "/tasks/{taskId:int}/result";

    public static string TaskResult(int taskId) => $"{Prefix}/tasks/{taskId}/result";

    /// <summary>Größter erlaubter Bericht. Ein normaler Bericht hat wenige Kilobyte.</summary>
    public const int MaxReportBytes = 1024 * 1024;

    /// <summary>JSON-Einstellungen für beide Seiten: Namen in camelCase, Aufzählungen als Text („Install“, „Warning“).</summary>
    public static JsonSerializerOptions JsonOptions { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>Gleiche Einstellungen für die Minimal-API des Servers.</summary>
    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        // Zuerst: Aufgabentypen, die diese Version nicht kennt, werden zu „Unknown“ statt zu einem Fehler für die ganze Liste
        options.Converters.Add(new TolerantEnumConverter<AgentTaskKind>(AgentTaskKind.Unknown));
        options.Converters.Add(new JsonStringEnumConverter());
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        return options;
    }
}

/// <summary>Liest eine Aufzählung als Text; unbekannte Namen ergeben <paramref name="fallback"/> statt einer Ausnahme.</summary>
public sealed class TolerantEnumConverter<TEnum>(TEnum fallback) : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Nur genau bekannte Namen; Zahlen oder fremde Namen ergeben den Rückfallwert
        var name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return name is not null && Enum.GetNames<TEnum>().Contains(name, StringComparer.Ordinal) ? Enum.Parse<TEnum>(name) : fallback;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>Einmal-Code aus der Verwaltung gegen einen Geräte-Token tauschen.</summary>
public sealed record RegisterRequest(string Code, string ComputerName);

/// <param name="Token">Geheimer Token; der Server speichert nur seinen Hash. Wird nur einmal übertragen.</param>
public sealed record RegisterResponse(int DeviceId, string Token);

/// <summary>Bericht eines Geräts: derselbe Inhalt wie der JSON-Export der App plus die installierten Programme.</summary>
/// <param name="Installed">Programme laut winget; <c>null</c> = konnte nicht gelesen werden.</param>
/// <param name="Agent">Version und Fähigkeiten des Agents; <c>null</c> bei Agents vor Version 4.</param>
public sealed record ReportRequest(string ComputerName, SystemReport Report, IReadOnlyList<InstalledProgram>? Installed, AgentInfo? Agent = null);

/// <param name="Version">Version des Agents, z. B. „4.0.0“.</param>
/// <param name="SupportedTasks">Aufgabentypen, die dieser Agent ausführen kann. Der Server bietet nur diese an.</param>
public sealed record AgentInfo(string Version, IReadOnlyList<AgentTaskKind> SupportedTasks);

public sealed record InstalledProgram(string Id, string Version);

/// <summary>Was ein Agent ausführen darf. Feste Liste – der Server kann keine beliebigen Befehle schicken.</summary>
public enum AgentTaskKind
{
    /// <summary>Programm über winget installieren.</summary>
    Install,

    /// <summary>Programm über winget aktualisieren.</summary>
    Upgrade,

    /// <summary>
    /// Den Agent selbst aktualisieren (seit 4.0). Ohne Parameter: Adresse, Version und Echtheit bestimmt der Agent selbst
    /// (feste Release-Seite, eingebaute Schlüssel) – der Server kann damit keine fremde Datei schicken.
    /// </summary>
    SelfUpdate,

    /// <summary>Ein Typ aus einer neueren Version, den dieser Agent nicht kennt. Wird nie ausgeführt, nur als „nicht unterstützt“ gemeldet.</summary>
    Unknown = -1,
}

/// <summary>Was ein Agent vor Version 4 konnte (er meldete seine Fähigkeiten noch nicht).</summary>
public static class AgentTaskKinds
{
    public static IReadOnlyList<AgentTaskKind> Legacy { get; } = [AgentTaskKind.Install, AgentTaskKind.Upgrade];
}

/// <summary>Eine freigegebene Aufgabe für den Agent.</summary>
/// <param name="PackageId">winget-Paket-ID bei Install/Upgrade; leer bei <see cref="AgentTaskKind.SelfUpdate"/>.</param>
public sealed record AgentTask(int Id, AgentTaskKind Kind, string PackageId);

/// <param name="Message">Kurzer Text für die Verwaltung, z. B. „fertig“ oder „keine Internetverbindung“.</param>
public sealed record TaskResultRequest(bool Success, int ExitCode, string Message);
