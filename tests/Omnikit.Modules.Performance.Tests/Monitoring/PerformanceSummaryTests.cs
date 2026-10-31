using Omnikit.Core.Modules;
using Omnikit.Modules.Performance.Monitoring;
using Omnikit.Modules.Performance.Resources;

namespace Omnikit.Modules.Performance.Tests.Monitoring;

public class PerformanceSummaryTests
{
    [Fact]
    public void AllCalmIsGreen()
    {
        var summary = PerformanceSummary.Build(hasEnoughData: true, cpuHigh: false, memoryHigh: false, busyDisks: []);

        Assert.Equal(Severity.Ok, summary.Severity);
        Assert.Equal(PerformanceTexts.Summary_Ok, summary.Text);
    }

    [Fact]
    public void BeforeThirtySecondsItSaysItIsStillMeasuring()
    {
        var summary = PerformanceSummary.Build(hasEnoughData: false, cpuHigh: false, memoryHigh: false, busyDisks: []);

        Assert.Equal(PerformanceTexts.Summary_Collecting, summary.Text);
    }

    [Fact]
    public void EveryBusyPartGetsItsOwnHint()
    {
        var summary = PerformanceSummary.Build(hasEnoughData: true, cpuHigh: true, memoryHigh: true, busyDisks: ["C:"]);

        Assert.Equal(Severity.Info, summary.Severity);
        Assert.Contains(PerformanceTexts.Hint_Cpu, summary.Text, StringComparison.Ordinal);
        Assert.Contains(PerformanceTexts.Hint_Memory, summary.Text, StringComparison.Ordinal);
        Assert.Contains("C:", summary.Text, StringComparison.Ordinal);
    }
}
