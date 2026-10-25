using PcTool.Core.Modules;

namespace PcTool.Core.Tests.Modules;

public class ModuleReportTests
{
    [Fact]
    public void OverallSeverityIsOkWithoutFindings()
    {
        var report = new ModuleReport("test", []);

        Assert.Equal(Severity.Ok, report.OverallSeverity);
    }

    [Fact]
    public void OverallSeverityIsWorstFinding()
    {
        var report = new ModuleReport("test",
        [
            new Finding("A", "", Severity.Info),
            new Finding("B", "", Severity.Critical),
            new Finding("C", "", Severity.Warning),
        ]);

        Assert.Equal(Severity.Critical, report.OverallSeverity);
    }
}
