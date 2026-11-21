using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Bios.Data;
using Omnikit.Modules.Bios.Evaluation;
using Omnikit.Modules.Bios.Resources;
using Omnikit.Modules.Bios.Sources;

namespace Omnikit.Modules.Bios.Tests;

public class FirmwareEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);
    private static readonly FirmwareInfo Laptop = new("LENOVO", "KYCN38WW", new DateOnly(2025, 3, 10), FirmwareMode.Uefi, true, "LENOVO", "LNVNB161216");
    private static readonly SupportPage Lenovo = new("Lenovo", new Uri("https://pcsupport.lenovo.com/"));

    private static Finding Titled(IReadOnlyList<Finding> findings, string title) => Assert.Single(findings, finding => finding.Title == title);

    [Fact]
    public void ModernLaptopIsGreen()
    {
        var findings = FirmwareEvaluator.Evaluate(Laptop, Lenovo, isVm: false, Today);

        Assert.All(findings, finding => Assert.Equal(Severity.Ok, finding.Severity));
        Assert.Contains("KYCN38WW", Titled(findings, BiosTexts.Version_Title).Explanation, StringComparison.Ordinal);
        Assert.Contains("Lenovo", Titled(findings, BiosTexts.Updates_Title).Explanation, StringComparison.Ordinal);
        Assert.Equal("https://pcsupport.lenovo.com/", Titled(findings, BiosTexts.Updates_Title).Details);
    }

    [Fact]
    public void SecureBootOffIsAWarningOnRealPcs()
    {
        var findings = FirmwareEvaluator.Evaluate(Laptop with { SecureBootEnabled = false }, Lenovo, isVm: false, Today);

        Assert.Equal(Severity.Warning, Titled(findings, BiosTexts.SecureBoot_Title).Severity);
    }

    [Fact]
    public void OldBiosOnlyGetsAHint()
    {
        var findings = FirmwareEvaluator.Evaluate(Laptop with { ReleaseDate = new DateOnly(2021, 1, 5) }, Lenovo, isVm: false, Today);

        Assert.Equal(Severity.Info, Titled(findings, BiosTexts.Old_Title).Severity);
        Assert.DoesNotContain(findings, finding => finding.Severity > Severity.Info);
    }

    [Fact]
    public void VmWithLegacyBiosShowsHintsButNoUpdateAdvice()
    {
        var seaBios = new FirmwareInfo("SeaBIOS", "rel-1.17.0", new DateOnly(2014, 4, 1), FirmwareMode.Legacy, null, null, null);

        var findings = FirmwareEvaluator.Evaluate(seaBios, null, isVm: true, Today);

        Assert.Equal(BiosTexts.Mode_LegacyVm, Titled(findings, BiosTexts.Mode_Title).Explanation);
        Assert.DoesNotContain(findings, finding => finding.Title == BiosTexts.Old_Title);
        Assert.DoesNotContain(findings, finding => finding.Title == BiosTexts.Updates_Title);
        Assert.DoesNotContain(findings, finding => finding.Title == BiosTexts.SecureBoot_Title);
    }

    [Fact]
    public void UnknownManufacturerGetsGeneralAdviceWithoutLink()
    {
        var findings = FirmwareEvaluator.Evaluate(Laptop, null, isVm: false, Today);

        var updates = Titled(findings, BiosTexts.Updates_Title);
        Assert.Equal(BiosTexts.Updates_Unknown, updates.Explanation);
        Assert.Null(updates.Details);
    }
}

public class SupportPagesTests
{
    [Theory]
    [InlineData("LENOVO", "21DH", null, "Lenovo")]
    [InlineData("HP", "HP ProBook 450", null, "HP")]
    [InlineData("Hewlett-Packard", "HP Compaq", null, "HP")]
    [InlineData("Dell Inc.", "Latitude 5420", null, "Dell")]
    [InlineData("ASUSTeK COMPUTER INC.", "ZenBook", null, "ASUS")]
    [InlineData("System manufacturer", "System Product Name", "ASUSTeK COMPUTER INC.", "ASUS")]
    [InlineData("To Be Filled By O.E.M.", "To Be Filled By O.E.M.", "ASRock", "ASRock")]
    [InlineData("Micro-Star International Co., Ltd.", "MS-7C02", null, "MSI")]
    [InlineData("Microsoft Corporation", "Surface Laptop 5", null, "Microsoft Surface")]
    public void KnownManufacturersHaveTheirOfficialPage(string manufacturer, string model, string? board, string expected)
    {
        Assert.Equal(expected, SupportPages.Find(manufacturer, model, board)?.Name);
    }

    [Theory]
    [InlineData("Microsoft Corporation", "Virtual Machine", null)]
    [InlineData("QEMU", "Standard PC (i440FX + PIIX, 1996)", null)]
    [InlineData("Kleiner Händler GmbH", "Office PC", null)]
    [InlineData(null, null, null)]
    public void OthersGetNoGuessedLink(string? manufacturer, string? model, string? board)
    {
        Assert.Null(SupportPages.Find(manufacturer, model, board));
    }

    [Fact]
    public void AllLinksUseHttps()
    {
        foreach (var name in new[] { "LENOVO", "HP", "Dell", "ASUS", "Acer", "Micro-Star", "GIGABYTE", "ASRock" })
        {
            Assert.Equal("https", SupportPages.Find(name, "x", null)!.Url.Scheme);
        }
    }
}

public class BiosModuleTests
{
    private sealed class FakeFirmware(FirmwareInfo? firmware) : IFirmwareSource
    {
        public FirmwareInfo? Read() => firmware;
    }

    private static readonly FirmwareInfo Laptop = new("LENOVO", "KYCN38WW", new DateOnly(2025, 3, 10), FirmwareMode.Uefi, true, "LENOVO", "X");

    private static BiosModule Create(FirmwareInfo? firmware) => new(new FakeFirmware(firmware), TimeProvider.System, NullLogger<BiosModule>.Instance);

    [Fact]
    public async Task KnownManufacturerOffersItsSupportPage()
    {
        var module = Create(Laptop);
        var report = await module.CheckAsync(new SystemContext { IsWindows = true, VmType = VmType.None, Manufacturer = "LENOVO" }, CancellationToken.None);

        var action = Assert.Single(module.GetActions(report));
        Assert.Contains("pcsupport.lenovo.com", action.ConfirmationText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VirtualMachinesGetNoSupportPage()
    {
        var module = Create(Laptop);
        var report = await module.CheckAsync(new SystemContext { IsWindows = true, VmType = VmType.Qemu, Manufacturer = "LENOVO" }, CancellationToken.None);

        Assert.Empty(module.GetActions(report));
    }

    [Fact]
    public async Task UnreadableFirmwareIsReported()
    {
        var report = await Create(null).CheckAsync(new SystemContext { IsWindows = true }, CancellationToken.None);

        Assert.Equal(BiosTexts.Common_ReadFailed, Assert.Single(report.Findings).Explanation);
    }

    [Theory]
    [InlineData("20240315000000.000000+000", 2024, 3, 15)]
    public void WmiDatesAreParsed(string value, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), WindowsFirmwareSource.ParseDate(value));
    }
}
