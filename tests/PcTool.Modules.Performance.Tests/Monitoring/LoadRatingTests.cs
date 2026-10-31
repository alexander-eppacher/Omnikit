using PcTool.Core.Modules;
using PcTool.Modules.Performance.Monitoring;

namespace PcTool.Modules.Performance.Tests.Monitoring;

public class LoadRatingTests
{
    private static LoadHistory HistoryOf(params double[] values)
    {
        var history = new LoadHistory();
        foreach (var value in values)
        {
            history.Add(value);
        }

        return history;
    }

    private static double[] Seconds(int count, double value) => Enumerable.Repeat(value, count).ToArray();

    [Fact]
    public void ThirtySecondsOfFullLoadGiveHint()
    {
        Assert.Equal(Severity.Info, LoadRating.Rate(HistoryOf(Seconds(30, 95))));
    }

    [Fact]
    public void ShortPeakGivesNoHint()
    {
        // 25 ruhige Sekunden, dann 5 Sekunden Vollgas: Durchschnitt 25 %
        var values = Seconds(25, 10).Concat(Seconds(5, 100)).ToArray();

        Assert.Equal(Severity.Ok, LoadRating.Rate(HistoryOf(values)));
    }

    [Fact]
    public void FullLoadForLessThanThirtySecondsGivesNoHintYet()
    {
        Assert.Equal(Severity.Ok, LoadRating.Rate(HistoryOf(Seconds(29, 100))));
        Assert.False(LoadRating.HasEnoughData(HistoryOf(Seconds(29, 100))));
    }

    [Theory]
    [InlineData(89.9, Severity.Ok)]
    [InlineData(90, Severity.Info)] // Grenze: ab 90 % Hinweis
    public void ThresholdIsNinetyPercent(double average, Severity expected)
    {
        Assert.Equal(expected, LoadRating.Rate(HistoryOf(Seconds(30, average))));
    }

    [Fact]
    public void OnlyTheLastThirtySecondsCount()
    {
        // Früher Vollgas, jetzt seit 30 Sekunden ruhig
        var values = Seconds(30, 100).Concat(Seconds(30, 5)).ToArray();

        Assert.Equal(Severity.Ok, LoadRating.Rate(HistoryOf(values)));
    }

    private static readonly int[] OneValuePerWord = [5, 50, 80, 95];

    [Fact]
    public void WordsGetStrongerWithLoad()
    {
        var words = OneValuePerWord.Select(percent => LoadRating.Describe(percent)).ToList();

        Assert.Equal(4, words.Distinct().Count());
    }
}
