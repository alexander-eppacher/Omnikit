using PcTool.Core.Modules;
using PcTool.Modules.Health.Data;
using PcTool.Modules.Health.Evaluation;
using PcTool.Modules.Health.Resources;
using static PcTool.Modules.Health.Evaluation.TextFormat;

namespace PcTool.Modules.Health.Tests.Evaluation;

public class StorageEvaluatorTests
{
    private const long GB = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(100, 500, Severity.Ok)]      // 20 %
    [InlineData(75, 500, Severity.Ok)]       // 15 % = Grenze, noch grün
    [InlineData(70, 500, Severity.Warning)]  // 14 %
    [InlineData(25, 500, Severity.Warning)]  // 5 %
    [InlineData(24, 500, Severity.Critical)] // unter 5 %
    [InlineData(4, 50, Severity.Critical)]   // 8 %, aber unter 5 GB
    [InlineData(0, 500, Severity.Critical)]
    public void FreeSpaceThresholds(long freeGb, long totalGb, Severity expected)
    {
        Assert.Equal(expected, StorageEvaluator.Rate(freeGb * GB, totalGb * GB));
    }

    [Fact]
    public void FindingShowsReadableSizes()
    {
        var finding = StorageEvaluator.Evaluate(new VolumeInfo(@"C:\", 500 * GB, 100 * GB));

        Assert.Contains("GB", finding.Explanation, StringComparison.Ordinal);
        Assert.Contains("20", finding.Explanation, StringComparison.Ordinal);
    }
}

public class BatteryEvaluatorTests
{
    [Theory]
    [InlineData(100, Severity.Ok)]
    [InlineData(80, Severity.Ok)]
    [InlineData(79, Severity.Warning)]
    [InlineData(50, Severity.Warning)]
    [InlineData(49, Severity.Critical)]
    public void CapacityThresholds(int percent, Severity expected)
    {
        Assert.Equal(expected, BatteryEvaluator.RateCapacity(percent));
    }

    [Fact]
    public void DevelopersLaptopBatteryIsGreen()
    {
        // Echte Werte vom ThinkBook: 42 090 von 45 000 mWh, 179 Ladezyklen → 94 %
        var findings = BatteryEvaluator.Evaluate(new BatteryInfo("Test", 45000, 42090, 179));

        Assert.Equal(2, findings.Count);
        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
        Assert.Contains("94", findings[0].Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void CapacityAboveDesignShowsHundredPercent()
    {
        var findings = BatteryEvaluator.Evaluate(new BatteryInfo(null, 45000, 46000, null));

        Assert.Contains("100", Assert.Single(findings).Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingValuesGiveNoFindings()
    {
        Assert.Empty(BatteryEvaluator.Evaluate(new BatteryInfo(null, null, null, null)));
        Assert.Empty(BatteryEvaluator.Evaluate(new BatteryInfo(null, 0, 1000, 0)));
    }
}

public class WindowsStateEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(0, Severity.Ok)]
    [InlineData(14, Severity.Ok)]
    [InlineData(15, Severity.Info)]
    [InlineData(30, Severity.Info)]
    [InlineData(31, Severity.Warning)]
    public void UptimeThresholds(int days, Severity expected)
    {
        Assert.Equal(expected, WindowsStateEvaluator.RateUptime(days));
    }

    [Fact]
    public void UptimeTextSaysTodayAndYesterday()
    {
        Assert.Equal(HealthTexts.Uptime_Today, WindowsStateEvaluator.Evaluate(new WindowsState(Now.AddHours(-3), null, null), Now)[0].Explanation);
        Assert.Equal(HealthTexts.Uptime_Yesterday, WindowsStateEvaluator.Evaluate(new WindowsState(Now.AddDays(-1), null, null), Now)[0].Explanation);
        Assert.Equal(Format(HealthTexts.Uptime_Ok, 5), WindowsStateEvaluator.Evaluate(new WindowsState(Now.AddDays(-5), null, null), Now)[0].Explanation);
    }

    [Fact]
    public void FreshAndActivatedWindowsIsGreen()
    {
        var findings = WindowsStateEvaluator.Evaluate(new WindowsState(Now.AddDays(-2), false, true), Now);

        Assert.Equal(2, findings.Count); // Neustart, Aktivierung (kein "Neustart ausstehend")
        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
    }

    [Fact]
    public void NotActivatedGivesWarning()
    {
        var findings = WindowsStateEvaluator.Evaluate(new WindowsState(Now, null, false), Now);

        Assert.Contains(findings, finding => finding.Severity == Severity.Warning);
    }

    [Fact]
    public void PendingRebootGivesInfo()
    {
        var findings = WindowsStateEvaluator.Evaluate(new WindowsState(Now, true, true), Now);

        Assert.Equal(3, findings.Count);
        Assert.Contains(findings, finding => finding.Severity == Severity.Info);
    }

    [Fact]
    public void UnknownValuesAreInfoNotWarning()
    {
        var findings = WindowsStateEvaluator.Evaluate(new WindowsState(null, null, null), Now);

        Assert.All(findings, finding => Assert.Equal(Severity.Info, finding.Severity));
    }

    [Theory]
    [InlineData(0, 0, Severity.Ok)]
    [InlineData(3, 0, Severity.Info)]
    [InlineData(3, 1, Severity.Warning)]
    public void UpdatesAreRated(int total, int security, Severity expected)
    {
        Assert.Equal(expected, WindowsStateEvaluator.EvaluateUpdates(new UpdateStatus(total, security)).Severity);
    }

    [Fact]
    public void UpdateSearchNotPossibleIsInfo()
    {
        Assert.Equal(Severity.Info, WindowsStateEvaluator.EvaluateUpdates(null).Severity);
    }
}
