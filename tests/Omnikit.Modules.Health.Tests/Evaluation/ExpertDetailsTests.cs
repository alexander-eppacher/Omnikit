using Omnikit.Core.Modules;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Evaluation;
using Omnikit.Modules.Health.Sources;
using static Omnikit.Modules.Health.Evaluation.TextFormat;

namespace Omnikit.Modules.Health.Tests.Evaluation;

/// <summary>
/// Rohwerte (<see cref="Finding.Details"/>) und Expertenbefunde für die Expertenansicht.
/// </summary>
public class ExpertDetailsTests
{
    private static readonly DiskInfo Ssd = new("0", "Test-SSD", DiskMediaType.Ssd, 512L * 1024 * 1024 * 1024, DiskHealthStatus.Healthy, "NVMe", "7L2QFXV7");

    [Fact]
    public void DetailsSkipMissingValues()
    {
        Assert.Equal("A · C", Details("A", null, "C", ""));
        Assert.Null(Details(null, null));
    }

    [Fact]
    public void DiskStatusShowsProfile()
    {
        var status = DiskEvaluator.Evaluate(Ssd, null)[0];

        Assert.NotNull(status.Details);
        Assert.Contains("NVMe", status.Details, StringComparison.Ordinal);
        Assert.Contains("SSD", status.Details, StringComparison.Ordinal);
        Assert.Contains("GB", status.Details, StringComparison.Ordinal);
        Assert.Contains("7L2QFXV7", status.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void DiskWithoutProfileValuesHasNoDetails()
    {
        var unknown = new DiskInfo("0", "?", DiskMediaType.Unknown, null, DiskHealthStatus.Healthy);

        Assert.Null(DiskEvaluator.Evaluate(unknown, null)[0].Details);
    }

    [Fact]
    public void TemperatureShowsMaxOnlyWhenReported()
    {
        var withMax = DiskEvaluator.Evaluate(Ssd, new DiskReliability("0", 42, null, null, null, null, 70))[1];
        var withoutMax = DiskEvaluator.Evaluate(Ssd, new DiskReliability("0", 42, null, null, null, null, 0))[1];

        Assert.Contains("70", withMax.Details, StringComparison.Ordinal);
        Assert.Equal(Severity.Ok, withMax.Severity); // TemperatureMax wird nicht bewertet
        Assert.DoesNotContain("TemperatureMax", withoutMax.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorsShowReadAndWriteSeparately()
    {
        var errors = DiskEvaluator.Evaluate(Ssd, new DiskReliability("0", null, null, null, 2, 1))[1];

        Assert.Contains("2", errors.Details, StringComparison.Ordinal);
        Assert.Contains("1", errors.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void PowerOnHoursAreExpertOnlyAndNeverWorseThanOk()
    {
        var findings = DiskEvaluator.Evaluate(Ssd, new DiskReliability("0", null, null, 1200, null, null));

        var powerOn = Assert.Single(findings, finding => finding.IsExpertOnly);
        Assert.Equal(Severity.Ok, powerOn.Severity);
    }

    [Fact]
    public void StorageShowsFileSystem()
    {
        var finding = StorageEvaluator.Evaluate(new VolumeInfo(@"C:\", 500L << 30, 100L << 30, "NTFS"));

        Assert.Contains("NTFS", finding.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void BatteryCapacityInMilliwattHoursOnlyInDetails()
    {
        var capacity = BatteryEvaluator.Evaluate(new BatteryInfo("Test", 45000, 42090, 179))[0];

        Assert.DoesNotContain("mWh", capacity.Explanation, StringComparison.Ordinal);
        Assert.Contains("mWh", capacity.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(17L, "NVMe")]
    [InlineData(11L, "SATA")]
    [InlineData(7L, "USB")]
    [InlineData(0L, null)]
    [InlineData(null, null)]
    public void BusTypeNames(long? busType, string? expected)
    {
        Assert.Equal(expected, WindowsDiskSource.ToBusName(busType));
    }
}
