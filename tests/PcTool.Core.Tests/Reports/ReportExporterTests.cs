using System.Text;
using System.Text.Json;
using PcTool.Core.Modules;
using PcTool.Core.Reports;
using PcTool.Core.SystemInfo;

namespace PcTool.Core.Tests.Reports;

public class ReportExporterTests
{
    private static readonly SystemContext Context = new()
    {
        OsName = "Microsoft Windows 11 Pro",
        Model = "ThinkBook 14 G7 ARP (21MV)",
        SerialNumber = "GEHEIM123",
        VmType = VmType.None,
        IsWindows = true,
    };

    private static async Task<string> ExportAsync(params ModuleReport[] reports)
    {
        using var stream = new MemoryStream();
        await ReportExporter.WriteAsync(stream, Context, reports, new DateTimeOffset(2026, 10, 24, 15, 30, 0, TimeSpan.Zero), CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public async Task SerialNumberIsNotExported()
    {
        var json = await ExportAsync();

        Assert.DoesNotContain("GEHEIM123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SerialNumber", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContainsSystemInfoAndFindings()
    {
        var report = new ModuleReport("health", [new Finding("SSD", "Alles gut", Severity.Warning, "Laufwerke")]);

        using var document = JsonDocument.Parse(await ExportAsync(report));
        var root = document.RootElement;

        Assert.Equal("ThinkBook 14 G7 ARP (21MV)", root.GetProperty("System").GetProperty("Model").GetString());
        var module = root.GetProperty("Modules")[0];
        Assert.Equal("health", module.GetProperty("ModuleId").GetString());
        Assert.Equal("Warning", module.GetProperty("OverallSeverity").GetString()); // als Text, nicht als Zahl
        Assert.Equal("Laufwerke", module.GetProperty("Findings")[0].GetProperty("Category").GetString());
    }

    [Fact]
    public void FileNameContainsDate()
    {
        Assert.Equal(
            "PcTool-Bericht-2026-10-24_1530.json",
            ReportExporter.SuggestFileName(new DateTimeOffset(2026, 10, 24, 15, 30, 0, TimeSpan.Zero)));
    }
}
