using System.Globalization;
using Omnikit.Modules.Performance.Data;
using Omnikit.Modules.Performance.Monitoring;

namespace Omnikit.Modules.Performance.Tests.Monitoring;

public class NetworkAdapterFilterTests
{
    private static NetworkSample Adapter(string description, bool connected = true) =>
        new("id", "Ethernet", description, connected, false, NetworkAdapterFilter.IsVirtual(description), null, 0, 0, []);

    [Theory]
    [InlineData("Hyper-V Virtual Ethernet Adapter", true)]
    [InlineData("VirtualBox Host-Only Ethernet Adapter", true)]
    [InlineData("TAP-Windows Adapter V9", true)]
    [InlineData("WAN Miniport (IP)", true)]
    [InlineData("Intel(R) Wi-Fi 6E AX211 160MHz", false)]
    [InlineData("Realtek PCIe GbE Family Controller", false)]
    [InlineData("Microsoft Hyper-V Network Adapter", false)] // der echte Anschluss IN einer Hyper-V-VM
    [InlineData("Red Hat VirtIO Ethernet Adapter", false)]   // der echte Anschluss in einer Proxmox-VM
    public void VirtualAdaptersAreRecognized(string description, bool expected)
    {
        Assert.Equal(expected, NetworkAdapterFilter.IsVirtual(description));
    }

    [Theory]
    [InlineData("FastProtect Tunnel", false, true)]                                         // VPN: Windows meldet keine Hardware
    [InlineData("Cisco AnyConnect Virtual Miniport Adapter for Windows x64", false, true)]  // Name passt auf keine Liste
    [InlineData("Red Hat VirtIO Ethernet Adapter", true, false)]                            // Netzwerkkarte einer VM gilt als echt
    [InlineData("Cisco AnyConnect Virtual Miniport Adapter for Windows x64", null, false)]  // ohne Angabe zählt nur der Name
    [InlineData("Hyper-V Virtual Ethernet Adapter", null, true)]
    public void WindowsHardwareFlagDecidesFirst(string description, bool? isHardware, bool expected)
    {
        Assert.Equal(expected, NetworkAdapterFilter.IsVirtual(description, isHardware));
    }

    [Fact]
    public void SimpleViewShowsOnlyConnectedRealAdapters()
    {
        Assert.True(NetworkAdapterFilter.IsShownInSimpleView(Adapter("Realtek PCIe GbE Family Controller")));
        Assert.False(NetworkAdapterFilter.IsShownInSimpleView(Adapter("Realtek PCIe GbE Family Controller", connected: false)));
        Assert.False(NetworkAdapterFilter.IsShownInSimpleView(Adapter("Hyper-V Virtual Ethernet Adapter")));
    }

    [Fact]
    public void HiddenInterfacesAreRemovedWhenWindowsListIsKnown()
    {
        var visible = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) { ["{3D684D69-B7F8-4F1D-BFFD-D89F6865126B}"] = true };

        Assert.True(NetworkAdapterFilter.IsRealAdapter("{3d684d69-b7f8-4f1d-bffd-d89f6865126b}", visible));
        Assert.False(NetworkAdapterFilter.IsRealAdapter("{85870F58-6387-4BFD-979F-3916437D2320}", visible)); // Kernel-Debugger
    }

    [Fact]
    public void NothingIsRemovedWhenWindowsListIsUnavailable()
    {
        Assert.True(NetworkAdapterFilter.IsRealAdapter("{85870F58-6387-4BFD-979F-3916437D2320}", null));
    }
}

public class UnitsTests
{
    // Feste Kultur, damit die Tests auf jedem Rechner gleich laufen
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static string InGerman(Func<string> format)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = German;
        try
        {
            return format();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(512 * 1024, "512 KB/s")]
    [InlineData(12.3 * 1024 * 1024, "12,3 MB/s")]
    public void DiskThroughput(double bytes, string expected)
    {
        Assert.Equal(expected, InGerman(() => Units.BytesPerSecond(bytes)));
    }

    [Theory]
    [InlineData(0, "0 bit/s")]
    [InlineData(850_000, "850 kbit/s")]
    [InlineData(12_300_000, "12,3 Mbit/s")]
    [InlineData(1_000_000_000, "1,0 Gbit/s")]
    public void NetworkThroughputUsesBitsAndThousands(double bits, string expected)
    {
        Assert.Equal(expected, InGerman(() => Units.BitsPerSecond(bits)));
    }

    [Fact]
    public void MemoryLikeTaskManager()
    {
        Assert.Equal("27,3 GB", InGerman(() => Units.Bytes(27.3 * 1024 * 1024 * 1024)));
    }

    [Fact]
    public void ClockSpeedInGigahertz()
    {
        Assert.Equal("3,21 GHz", InGerman(() => Units.Megahertz(3210)));
    }

    [Theory]
    [InlineData(-3, "0 %")]
    [InlineData(23.4, "23 %")]
    [InlineData(130, "100 %")]
    public void PercentIsClamped(double value, string expected)
    {
        Assert.Equal(expected, InGerman(() => Units.Percent(value)));
    }

    [Fact]
    public void UptimeLikeTaskManager()
    {
        Assert.Equal("2:05:31:10", Units.Uptime(new TimeSpan(2, 5, 31, 10)));
    }
}
