using System.Text.Json;
using System.Text.Json.Serialization;
using PcTool.Core.Modules;
using PcTool.Core.SystemInfo;

namespace PcTool.Core.Reports;

/// <summary>
/// Schreibt einen Systembericht als lesbare JSON-Datei.
/// Datenschutz: Die Seriennummer wird bewusst NICHT exportiert.
/// </summary>
public static class ReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Ampelwerte als Text ("Warning") statt als Zahl, damit man die Datei ohne Doku versteht.
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task WriteAsync(
        Stream target, SystemContext context, IEnumerable<ModuleReport> reports, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        var report = new SystemReport(
            createdAt,
            typeof(ReportExporter).Assembly.GetName().Version?.ToString() ?? "unbekannt",
            ReportSystemInfo.From(context),
            reports.Select(ReportModule.From).ToList());

        await JsonSerializer.SerializeAsync(target, report, JsonOptions, cancellationToken);
    }

    /// <summary>Vorschlag für den Dateinamen, z. B. "PcTool-Bericht-2026-10-24_1530.json".</summary>
    public static string SuggestFileName(DateTimeOffset createdAt) =>
        $"PcTool-Bericht-{createdAt:yyyy-MM-dd_HHmm}.json";
}

internal sealed record SystemReport(
    DateTimeOffset CreatedAt,
    string AppVersion,
    ReportSystemInfo System,
    IReadOnlyList<ReportModule> Modules);

/// <summary>Systeminfos für den Bericht, ohne Seriennummer.</summary>
internal sealed record ReportSystemInfo(
    string? OsName,
    string? OsDisplayVersion,
    string? OsBuild,
    string? Manufacturer,
    string? Model,
    VmType? VmType,
    bool? IsLaptop,
    bool IsAdmin)
{
    public static ReportSystemInfo From(SystemContext context) => new(
        context.OsName,
        context.OsDisplayVersion,
        context.OsBuild,
        context.Manufacturer,
        context.Model,
        context.VmType,
        context.IsLaptop,
        context.IsAdmin);
}

internal sealed record ReportModule(string ModuleId, Severity OverallSeverity, IReadOnlyList<Finding> Findings)
{
    public static ReportModule From(ModuleReport report) => new(report.ModuleId, report.OverallSeverity, report.Findings);
}
