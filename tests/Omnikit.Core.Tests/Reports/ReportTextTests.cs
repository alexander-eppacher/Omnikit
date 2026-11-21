using Omnikit.Core.Modules;
using Omnikit.Core.Reports;
using Omnikit.Core.SystemInfo;

namespace Omnikit.Core.Tests.Reports;

public class ReportTextTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 11, 17, 30, 0, TimeSpan.Zero);

    private static string Text(SystemContext context, params Finding[] findings) =>
        ReportText.Create(ReportExporter.Create(context, [new ModuleReport("health", findings)], At), "Gerätezustand");

    [Fact]
    public void SystemAndFindingsAreReadableWithTheWorstFirst()
    {
        var context = new SystemContext
        {
            OsName = "Microsoft Windows 11 Pro", OsDisplayVersion = "24H2", OsBuild = "26100", Manufacturer = "LENOVO",
            Model = "ThinkBook 14", SerialNumber = "GEHEIM123", VmType = VmType.None, IsWindows = true,
        };

        var text = Text(context,
            new Finding("Speicherplatz", "Genug frei.", Severity.Ok),
            new Finding("SSD", "Noch ca. 85 % Lebensdauer.", Severity.Warning, Details: "Geschrieben: 40 TB\nTemperatur: 41 °C"));

        var lines = text.Split(Environment.NewLine);
        Assert.StartsWith("Omnikit – Gerätezustand (", lines[0], StringComparison.Ordinal);
        Assert.Equal("System: Microsoft Windows 11 Pro 24H2 · Build 26100 · LENOVO ThinkBook 14", lines[1]);
        Assert.Equal("Gesamt: Warnung", lines[2]);
        Assert.Contains("[Warnung] SSD", text, StringComparison.Ordinal);
        Assert.Contains("    Temperatur: 41 °C", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("[Warnung]", StringComparison.Ordinal) < text.IndexOf("[In Ordnung]", StringComparison.Ordinal));
        Assert.DoesNotContain("GEHEIM123", text, StringComparison.Ordinal);   // wie beim Export: nie die Seriennummer
    }

    [Fact]
    public void VirtualPcsAndMissingValuesAreHandled()
    {
        var text = Text(new SystemContext { VmType = VmType.Qemu, IsWindows = true });

        Assert.Contains("System: virtueller PC (Qemu)", text, StringComparison.Ordinal);
        Assert.Contains("Gesamt: In Ordnung", text, StringComparison.Ordinal);
    }
}
