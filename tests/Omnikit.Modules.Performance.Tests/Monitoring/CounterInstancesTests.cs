using Omnikit.Modules.Performance.Monitoring;

namespace Omnikit.Modules.Performance.Tests.Monitoring;

public class CounterInstancesTests
{
    private static KeyValuePair<string, double> Pair(string name, double value) => new(name, value);

    [Fact]
    public void CoresAreSortedNumericallyWithoutTotals()
    {
        var cores = CounterInstances.OrderCores(
        [
            Pair("_Total", 50), Pair("0,_Total", 50), Pair("0,10", 10), Pair("0,2", 2), Pair("0,0", 0), Pair("1,0", 100),
        ]);

        Assert.Equal([0, 2, 10, 100], cores);
    }

    [Theory]
    [InlineData("0 C:", 0, new[] { "C:" })]
    [InlineData("1 D: E:", 1, new[] { "D:", "E:" })]
    [InlineData("2", 2, new string[0])]
    public void DiskInstancesAreParsed(string instance, int number, string[] letters)
    {
        var disk = CounterInstances.ParseDisk(instance);

        Assert.NotNull(disk);
        Assert.Equal(number, disk.Value.Number);
        Assert.Equal(letters, disk.Value.Letters);
    }

    [Theory]
    [InlineData("_Total")]
    [InlineData("")]
    public void DiskTotalIsIgnored(string instance)
    {
        Assert.Null(CounterInstances.ParseDisk(instance));
    }

    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000D1B2_phys_0_eng_0_engtype_3D", "0x00000000_0x0000D1B2")]
    [InlineData("luid_0x00000000_0x0000d1b2_phys_0", "0x00000000_0x0000D1B2")]
    [InlineData("_Total", null)]
    public void GpuLuidIsParsed(string instance, string? expected)
    {
        Assert.Equal(expected, CounterInstances.ParseGpuLuid(instance));
    }

    [Fact]
    public void LuidFormatMatchesCounterInstances()
    {
        Assert.Equal("0x00000000_0x0000D1B2", CounterInstances.FormatLuid(0, 0xD1B2));
    }

    [Fact]
    public void GpuUsageIsBusiestEngineSummedOverPrograms()
    {
        const string Gpu = "luid_0x00000000_0x0000D1B2";
        var usage = CounterInstances.AggregateGpuUsage(
        [
            Pair($"pid_1_{Gpu}_phys_0_eng_0_engtype_3D", 20),
            Pair($"pid_2_{Gpu}_phys_0_eng_0_engtype_3D", 15), // gleiche 3D-Engine: 20 + 15 = 35
            Pair($"pid_3_{Gpu}_phys_0_eng_4_engtype_VideoDecode", 30),
            Pair("pid_1_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", 5), // zweite Grafikkarte
        ]);

        Assert.Equal(35, usage["0x00000000_0x0000D1B2"]);
        Assert.Equal(5, usage["0x00000000_0x0000AAAA"]);
    }

    [Fact]
    public void GpuUsageNeverExceedsHundred()
    {
        var usage = CounterInstances.AggregateGpuUsage(
        [
            Pair("pid_1_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3D", 80),
            Pair("pid_2_luid_0x00000000_0x00000001_phys_0_eng_0_engtype_3D", 70),
        ]);

        Assert.Equal(100, usage["0x00000000_0x00000001"]);
    }
}

public class NamingTests
{
    [Fact]
    public void DiskNamesAreReadable()
    {
        var c = new Omnikit.Modules.Performance.Data.DiskSample(0, ["C:"], 0, 0, 0);
        var noLetter = new Omnikit.Modules.Performance.Data.DiskSample(1, [], 0, 0, 0);

        Assert.Equal("Laufwerk C: (SSD)", Naming.Disk(c, Omnikit.Modules.Performance.Data.DiskKind.Ssd));
        Assert.Equal("Laufwerk C:", Naming.Disk(c, Omnikit.Modules.Performance.Data.DiskKind.Unknown));
        Assert.Equal("Datenträger 1 (Festplatte)", Naming.Disk(noLetter, Omnikit.Modules.Performance.Data.DiskKind.Hdd));
        Assert.Equal("C:", Naming.DiskShort(c));
    }
}

public class ReservedMemoryNoteTests
{
    private const long GB = 1024L * 1024 * 1024;

    [Fact]
    public void ExplainsReservedMemoryLikeOnTheThinkBook()
    {
        // 32 GB eingebaut, 27,3 GB nutzbar (Rest für die Grafik)
        Assert.NotNull(Naming.ReservedMemoryNote(32 * GB, 29_313_187_840));
    }

    [Fact]
    public void NoNoteWhenOnlyFewMegabytesAreReserved()
    {
        // Wie in der Proxmox-VM: 8 GB eingebaut, nur wenige MB weniger nutzbar
        Assert.Null(Naming.ReservedMemoryNote(8 * GB, (8 * GB) - (20L * 1024 * 1024)));
    }

    [Fact]
    public void NoNoteWhenInstalledMemoryIsUnknown()
    {
        Assert.Null(Naming.ReservedMemoryNote(null, 16 * GB));
    }
}
