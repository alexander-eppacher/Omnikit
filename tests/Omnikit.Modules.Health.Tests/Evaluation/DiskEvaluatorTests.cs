using Omnikit.Core.Modules;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Evaluation;

namespace Omnikit.Modules.Health.Tests.Evaluation;

public class DiskEvaluatorTests
{
    private static readonly DiskInfo HealthySsd = new("0", "Test-SSD", DiskMediaType.Ssd, 512_000_000_000, DiskHealthStatus.Healthy);

    [Theory]
    [InlineData(100, Severity.Ok)]
    [InlineData(30, Severity.Ok)]     // Grenze: ab 30 % grün
    [InlineData(29, Severity.Warning)]
    [InlineData(10, Severity.Warning)] // Grenze: ab 10 % gelb
    [InlineData(9, Severity.Critical)]
    [InlineData(0, Severity.Critical)]
    public void RemainingLifeThresholds(int remainingPercent, Severity expected)
    {
        Assert.Equal(expected, DiskEvaluator.RateRemainingLife(remainingPercent));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(15, 85)]
    [InlineData(100, 0)]
    [InlineData(130, 0)] // NVMe-SSDs können über 100 % Verschleiß melden
    public void RemainingLifeFromWear(int wear, int expectedRemaining)
    {
        Assert.Equal(expectedRemaining, DiskEvaluator.RemainingLife(wear));
    }

    [Theory]
    [InlineData(46, Severity.Ok)]
    [InlineData(60, Severity.Ok)]
    [InlineData(61, Severity.Warning)]
    [InlineData(70, Severity.Warning)]
    [InlineData(71, Severity.Critical)]
    public void TemperatureThresholds(int celsius, Severity expected)
    {
        Assert.Equal(expected, DiskEvaluator.RateTemperature(celsius));
    }

    [Theory]
    [InlineData(DiskHealthStatus.Healthy, Severity.Ok)]
    [InlineData(DiskHealthStatus.Warning, Severity.Warning)]
    [InlineData(DiskHealthStatus.Unhealthy, Severity.Critical)]
    [InlineData(DiskHealthStatus.Unknown, Severity.Info)]
    public void HealthStatusIsRated(DiskHealthStatus status, Severity expected)
    {
        var findings = DiskEvaluator.Evaluate(HealthySsd with { HealthStatus = status }, null);

        Assert.Equal(expected, Assert.Single(findings).Severity);
    }

    [Fact]
    public void ValuesLikeTheDevelopersLaptopAreAllGreen()
    {
        // Echte Werte vom ThinkBook: 46 °C, 0 % Verschleiß, Betriebsstunden und Fehler nicht gemeldet.
        var reliability = new DiskReliability("0", 46, 0, null, null, null);

        var findings = DiskEvaluator.Evaluate(HealthySsd, reliability);

        Assert.Equal(3, findings.Count); // Zustand, Lebensdauer, Temperatur
        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
        Assert.Contains(findings, finding => finding.Explanation.Contains("100", StringComparison.Ordinal));
    }

    [Fact]
    public void WearIsIgnoredForHardDisks()
    {
        var hdd = HealthySsd with { MediaType = DiskMediaType.Hdd };

        var findings = DiskEvaluator.Evaluate(hdd, new DiskReliability("0", null, 95, null, null, null));

        Assert.Single(findings); // nur der Zustand
    }

    [Fact]
    public void ZeroDegreesMeansNoSensor()
    {
        var findings = DiskEvaluator.Evaluate(HealthySsd, new DiskReliability("0", 0, null, null, null, null));

        Assert.Single(findings);
    }

    [Fact]
    public void ErrorsGiveWarning()
    {
        var findings = DiskEvaluator.Evaluate(HealthySsd, new DiskReliability("0", null, null, null, 2, 1));

        Assert.Contains(findings, finding => finding.Severity == Severity.Warning && finding.Explanation.StartsWith('3'));
    }

    [Fact]
    public void ZeroErrorsAreOk()
    {
        var findings = DiskEvaluator.Evaluate(HealthySsd, new DiskReliability("0", null, null, null, 0, 0));

        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
        Assert.Equal(2, findings.Count);
    }
}
