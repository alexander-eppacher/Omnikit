using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Drivers.Data;
using Omnikit.Modules.Drivers.Resources;
using Omnikit.Modules.Drivers.Sources;

namespace Omnikit.Modules.Drivers.Tests;

public class DriversModuleTests
{
    private static readonly SystemContext Pc = new() { IsWindows = true, VmType = VmType.None };

    private sealed class FakeDevices(IReadOnlyList<DeviceInfo>? devices) : IDeviceSource
    {
        public IReadOnlyList<DeviceInfo>? ReadDevices() => devices;

        public IReadOnlyList<DriverInfo> ReadDrivers() => [];
    }

    private static DriversModule Create(IReadOnlyList<DeviceInfo>? devices) =>
        new(new FakeDevices(devices), NullLogger<DriversModule>.Instance);

    [Fact]
    public async Task WithoutProblemsOnlyTheDeviceManagerIsOffered()
    {
        var module = Create([new DeviceInfo("Tastatur", "Keyboard", @"HID\1", 0)]);

        var report = await module.CheckAsync(Pc, CancellationToken.None);

        Assert.Equal(Severity.Ok, report.OverallSeverity);
        Assert.Equal(DriversModule.OpenDeviceManagerActionId, Assert.Single(module.GetActions(report)).Id);
    }

    [Fact]
    public async Task ProblemsOfferToOpenOptionalUpdates()
    {
        var module = Create([new DeviceInfo("Unbekanntes Gerät", null, @"PCI\1", 28)]);

        var report = await module.CheckAsync(Pc, CancellationToken.None);

        Assert.Equal(Severity.Warning, report.OverallSeverity);
        Assert.Equal(
            [DriversModule.OpenOptionalUpdatesActionId, DriversModule.OpenDeviceManagerActionId],
            module.GetActions(report).Select(action => action.Id));
    }

    [Fact]
    public async Task RestartNeededStillOffersTheDeviceManager()
    {
        var module = Create([new DeviceInfo("USB-Stick", "USB", @"USB\1", 47)]);

        var report = await module.CheckAsync(Pc, CancellationToken.None);

        Assert.Equal(DriversModule.OpenDeviceManagerActionId, Assert.Single(module.GetActions(report)).Id);
    }

    [Fact]
    public async Task WithoutDeviceListThereIsNoAction()
    {
        var module = Create(null);

        Assert.Empty(module.GetActions(await module.CheckAsync(Pc, CancellationToken.None)));
        Assert.Empty(module.GetActions(await module.CheckAsync(new SystemContext { IsWindows = false }, CancellationToken.None)));
    }

    [Fact]
    public async Task UnreadableListIsReportedNotHidden()
    {
        var report = await Create(null).CheckAsync(Pc, CancellationToken.None);

        Assert.Equal(DriverTexts.Common_ReadFailed, Assert.Single(report.Findings).Explanation);
    }

    [Fact]
    public async Task OutsideWindowsNothingIsRead()
    {
        var report = await Create(null).CheckAsync(new SystemContext { IsWindows = false }, CancellationToken.None);

        Assert.Equal(DriverTexts.Common_NotWindows, Assert.Single(report.Findings).Explanation);
    }
}
