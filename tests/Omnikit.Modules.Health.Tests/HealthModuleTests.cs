using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Elevation;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Resources;
using Omnikit.Modules.Health.Tests.Fakes;

namespace Omnikit.Modules.Health.Tests;

public class HealthModuleTests
{
    private static readonly SystemContext Laptop = new()
    {
        IsWindows = true,
        VmType = VmType.None,
        IsLaptop = true,
        IsAdmin = false,
        IsAdminAccount = true,
    };

    private readonly FakeSources _sources = new();

    private HealthModule CreateModule() => new(
        _sources, _sources, _sources, _sources, _sources, _sources, _sources,
        TimeProvider.System, NullLogger<HealthModule>.Instance);

    private static IEnumerable<Finding> InCategory(ModuleReport report, string category) =>
        report.Findings.Where(finding => finding.Category == category);

    [Fact]
    public async Task HealthyLaptopIsGreen()
    {
        var report = await CreateModule().CheckAsync(Laptop, CancellationToken.None);

        Assert.Equal(Severity.Ok, report.OverallSeverity);
        Assert.NotEmpty(InCategory(report, HealthTexts.Category_Disks));
        Assert.NotEmpty(InCategory(report, HealthTexts.Category_Battery));
        Assert.NotEmpty(InCategory(report, HealthTexts.Category_Storage));
        Assert.NotEmpty(InCategory(report, HealthTexts.Category_Windows));
    }

    [Fact]
    public async Task InVmBatteryAndDiskDetailsAreNotAvailable()
    {
        var vm = Laptop with { VmType = VmType.HyperV };

        var report = await CreateModule().CheckAsync(vm, CancellationToken.None);

        Assert.Equal(HealthTexts.Disk_Vm, Assert.Single(InCategory(report, HealthTexts.Category_Disks)).Explanation);
        Assert.Equal(HealthTexts.Battery_Vm, Assert.Single(InCategory(report, HealthTexts.Category_Battery)).Explanation);
        Assert.NotEmpty(InCategory(report, HealthTexts.Category_Storage)); // Speicherplatz ist auch in VMs sinnvoll
    }

    [Fact]
    public async Task DesktopPcHasNoBatterySection()
    {
        var desktop = Laptop with { IsLaptop = false };

        var report = await CreateModule().CheckAsync(desktop, CancellationToken.None);

        Assert.Empty(InCategory(report, HealthTexts.Category_Battery));
    }

    [Fact]
    public async Task OnLinuxWindowsPartsAreNotAvailable()
    {
        var linux = new SystemContext { IsWindows = false };

        var report = await CreateModule().CheckAsync(linux, CancellationToken.None);

        Assert.Equal(HealthTexts.Common_NotWindows, Assert.Single(InCategory(report, HealthTexts.Category_Disks)).Explanation);
        Assert.Equal(HealthTexts.Common_NotWindows, Assert.Single(InCategory(report, HealthTexts.Category_Windows)).Explanation);
    }

    [Fact]
    public async Task WithoutAdminRightsTheActionIsOffered()
    {
        _sources.Reliability = null; // Zugriff verweigert
        var module = CreateModule();

        var report = await module.CheckAsync(Laptop, CancellationToken.None);

        var action = Assert.Single(module.GetActions(report));
        Assert.Equal(HealthModule.LoadDiskDetailsActionId, action.Id);
        Assert.Contains(report.Findings, finding => finding.Title == HealthTexts.Disk_DetailsNeedAdmin_Title);
    }

    [Fact]
    public async Task EmptyResultWithoutAdminAlsoOffersTheAction()
    {
        _sources.Reliability = []; // manche Windows-Versionen liefern ohne Rechte einfach nichts
        var module = CreateModule();

        var report = await module.CheckAsync(Laptop, CancellationToken.None);

        Assert.Single(module.GetActions(report));
    }

    [Fact]
    public async Task WhenDetailsAreReadableNoActionIsOffered()
    {
        var module = CreateModule();

        var report = await module.CheckAsync(Laptop, CancellationToken.None);

        Assert.Empty(module.GetActions(report));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmationTextDependsOnAccountType(bool isAdminAccount)
    {
        _sources.Reliability = null;
        var module = CreateModule();

        var report = await module.CheckAsync(Laptop with { IsAdminAccount = isAdminAccount }, CancellationToken.None);
        var action = Assert.Single(module.GetActions(report));

        var expected = isAdminAccount ? HealthTexts.Action_Confirm_AdminAccount : HealthTexts.Action_Confirm_StandardAccount;
        Assert.Equal(expected, action.ConfirmationText);
    }

    [Fact]
    public async Task CancelledElevationKeepsAppWorking()
    {
        _sources.Reliability = null;
        _sources.ElevationResult = new ElevationResult(ElevationStatus.Cancelled);
        var module = CreateModule();
        var action = Assert.Single(module.GetActions(await module.CheckAsync(Laptop, CancellationToken.None)));

        var result = await module.ExecuteAsync(action, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(HealthTexts.Action_Cancelled, result.Message);

        // Die Prüfung läuft weiter normal, die Aktion wird erneut angeboten.
        var report = await module.CheckAsync(Laptop, CancellationToken.None);
        Assert.Single(module.GetActions(report));
    }

    [Fact]
    public async Task SuccessfulElevationAddsDiskDetails()
    {
        _sources.Reliability = null;
        _sources.ElevationResult = new ElevationResult(
            ElevationStatus.Success,
            DiskReliabilityOperation.Serialize([new DiskReliability("0", 75, 95, null, null, null)]));
        var module = CreateModule();
        var action = Assert.Single(module.GetActions(await module.CheckAsync(Laptop, CancellationToken.None)));

        var result = await module.ExecuteAsync(action, CancellationToken.None);
        var report = await module.CheckAsync(Laptop, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(module.GetActions(report));
        Assert.Equal(Severity.Critical, report.OverallSeverity); // 75 °C und nur noch 5 % Lebensdauer
        Assert.Equal(1, _sources.ElevationCalls);
    }

    [Fact]
    public async Task InvalidElevationAnswerIsReportedAsFailure()
    {
        _sources.Reliability = null;
        _sources.ElevationResult = new ElevationResult(ElevationStatus.Success, "kein json");
        var module = CreateModule();
        var action = Assert.Single(module.GetActions(await module.CheckAsync(Laptop, CancellationToken.None)));

        var result = await module.ExecuteAsync(action, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(HealthTexts.Action_Failed, result.Message);
    }

    [Fact]
    public async Task SecurityUpdatesMakeOverallYellow()
    {
        _sources.Updates = new UpdateStatus(2, 1);

        var report = await CreateModule().CheckAsync(Laptop, CancellationToken.None);

        Assert.Equal(Severity.Warning, report.OverallSeverity);
    }
}
