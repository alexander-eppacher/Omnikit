using PcTool.Core.SystemInfo;
using PcTool.Core.Tests.Fakes;

namespace PcTool.Core.Tests.SystemInfo;

public class SystemContextBuilderTests
{
    private static SystemContext BuildFrom(RawSystemInfo raw) =>
        new SystemContextBuilder(new FakeSystemInfoProvider(raw)).Build();

    [Fact]
    public void CopiesValuesFromProvider()
    {
        var context = BuildFrom(new RawSystemInfo
        {
            OsName = "Microsoft Windows 11 Pro",
            OsDisplayVersion = "24H2",
            OsBuild = "26100.2314",
            Manufacturer = "LENOVO",
            Model = "20XW0026GE",
            SerialNumber = "PF3ABCDE",
            HasBattery = true,
            IsAdmin = true,
            IsWindows = true,
        });

        Assert.Equal("Microsoft Windows 11 Pro", context.OsName);
        Assert.Equal("24H2", context.OsDisplayVersion);
        Assert.Equal("26100.2314", context.OsBuild);
        Assert.Equal("LENOVO", context.Manufacturer);
        Assert.Equal("20XW0026GE", context.Model);
        Assert.Equal("PF3ABCDE", context.SerialNumber);
        Assert.True(context.IsLaptop);
        Assert.True(context.IsAdmin);
        Assert.True(context.IsWindows);
    }

    [Fact]
    public void DetectsVm()
    {
        var context = BuildFrom(new RawSystemInfo { Manufacturer = "Microsoft Corporation", Model = "Virtual Machine" });

        Assert.Equal(VmType.HyperV, context.VmType);
        Assert.True(context.IsVirtualMachine);
    }

    [Fact]
    public void RealPcIsNoVm()
    {
        var context = BuildFrom(new RawSystemInfo { Manufacturer = "Dell Inc.", Model = "OptiPlex 7090" });

        Assert.Equal(VmType.None, context.VmType);
        Assert.False(context.IsVirtualMachine);
    }

    [Fact]
    public void MissingDataStaysUnavailable()
    {
        // So sieht es unter Linux aus: keine Windows-Werte.
        var context = BuildFrom(new RawSystemInfo { IsWindows = false });

        Assert.Null(context.OsName);
        Assert.Null(context.Manufacturer);
        Assert.Null(context.VmType);
        Assert.Null(context.IsVirtualMachine);
        Assert.Null(context.IsLaptop);
        Assert.False(context.IsWindows);
    }

    [Fact]
    public void ManufacturerPlaceholdersBecomeUnavailable()
    {
        var context = BuildFrom(new RawSystemInfo
        {
            Manufacturer = "System manufacturer",
            Model = "System Product Name",
            SerialNumber = "To be filled by O.E.M.",
        });

        Assert.Null(context.Manufacturer);
        Assert.Null(context.Model);
        Assert.Null(context.SerialNumber);
        Assert.Null(context.VmType);
    }

    [Fact]
    public void LenovoModelGetsReadableName()
    {
        var context = BuildFrom(new RawSystemInfo
        {
            Manufacturer = "LENOVO",
            Model = "21MV",
            ProductVersion = "ThinkBook 14 G7 ARP",
        });

        Assert.Equal("ThinkBook 14 G7 ARP (21MV)", context.Model);
        Assert.Equal(VmType.None, context.VmType);
    }

    [Theory]
    [InlineData("LENOVO", "21MV", null, "21MV")]                      // Produktversion fehlt
    [InlineData("LENOVO", "21MV", "ThinkPad 21MV", "ThinkPad 21MV")]   // Typnummer steckt schon im Namen
    [InlineData("Dell Inc.", "OptiPlex 7090", "1.0", "OptiPlex 7090")] // andere Hersteller: unverändert
    [InlineData("HP", "HP EliteBook 840 G8", null, "HP EliteBook 840 G8")]
    [InlineData("LENOVO", null, "ThinkBook 14 G7 ARP", null)]         // ohne Modell nichts zusammensetzen
    public void BuildModelNameOnlyCombinesForLenovo(string? manufacturer, string? model, string? productVersion, string? expected)
    {
        Assert.Equal(expected, SystemContextBuilder.BuildModelName(manufacturer, model, productVersion));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Dell Inc.  ", "Dell Inc.")]
    [InlineData("Default string", null)]
    [InlineData("DEFAULT STRING", null)]
    [InlineData("0", null)]
    public void CleanTrimsAndRemovesPlaceholders(string? input, string? expected)
    {
        Assert.Equal(expected, SystemContextBuilder.Clean(input));
    }
}
