using PcTool.Modules.Performance.Monitoring;

namespace PcTool.Modules.Performance.Tests.Monitoring;

public class LoadHistoryTests
{
    [Fact]
    public void KeepsValuesInOrderOldestFirst()
    {
        var history = new LoadHistory(5);
        history.Add(1);
        history.Add(2);
        history.Add(3);

        Assert.Equal([1, 2, 3], history.ToArray());
    }

    [Fact]
    public void DropsOldestValueWhenFull()
    {
        var history = new LoadHistory(3);
        foreach (var value in new double[] { 1, 2, 3, 4, 5 })
        {
            history.Add(value);
        }

        Assert.Equal([3, 4, 5], history.ToArray());
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void AverageNeedsEnoughValues()
    {
        var history = new LoadHistory(60);
        history.Add(100);

        Assert.Null(history.AverageOfLast(30));
    }

    [Fact]
    public void AverageUsesOnlyNewestValues()
    {
        var history = new LoadHistory(4);
        foreach (var value in new double[] { 0, 0, 50, 100, 90 })
        {
            history.Add(value);
        }

        // Puffer: 0, 50, 100, 90 → die neuesten zwei sind 100 und 90
        Assert.Equal(95, history.AverageOfLast(2));
    }

    [Fact]
    public void ClearStartsOver()
    {
        var history = new LoadHistory(3);
        history.Add(1);
        history.Clear();

        Assert.Empty(history.ToArray());
    }
}
