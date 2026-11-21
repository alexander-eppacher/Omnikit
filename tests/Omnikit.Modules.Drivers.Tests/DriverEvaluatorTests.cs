using Omnikit.Core.Modules;
using Omnikit.Modules.Drivers.Data;
using Omnikit.Modules.Drivers.Evaluation;
using Omnikit.Modules.Drivers.Resources;
using Omnikit.Modules.Drivers.Sources;

namespace Omnikit.Modules.Drivers.Tests;

public class DriverEvaluatorTests
{
    private static readonly DeviceInfo Wifi = new("Intel(R) Wi-Fi 6 AX201", "Net", @"PCI\VEN_8086&DEV_A0F0", 0);
    private static readonly DriverInfo WifiDriver = new(@"PCI\VEN_8086&DEV_A0F0", "NET", "Intel(R) Wi-Fi 6 AX201", "Intel", "23.60.1.2", new DateOnly(2024, 5, 3), "oem42.inf");

    private static IEnumerable<Finding> Problems(IReadOnlyList<Finding> findings) =>
        findings.Where(finding => finding.Category == DriverTexts.Category_Problems);

    [Fact]
    public void HealthyPcHasOneGreenFinding()
    {
        var findings = DriverEvaluator.Evaluate([Wifi], [WifiDriver], isVm: false);

        var problem = Assert.Single(Problems(findings));
        Assert.Equal(Severity.Ok, problem.Severity);
        Assert.Contains("1", problem.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(28, DeviceProblem.NoDriver)]
    [InlineData(10, DeviceProblem.NotStarting)]
    [InlineData(43, DeviceProblem.NotStarting)]
    [InlineData(39, DeviceProblem.DriverBroken)]
    [InlineData(52, DeviceProblem.DriverBlocked)]
    [InlineData(12, DeviceProblem.Resources)]
    [InlineData(14, DeviceProblem.RestartNeeded)]
    [InlineData(22, DeviceProblem.Disabled)]
    [InlineData(45, DeviceProblem.None)]
    [InlineData(0, DeviceProblem.None)]
    [InlineData(99, DeviceProblem.Other)]
    public void ErrorCodesAreGrouped(int code, DeviceProblem expected)
    {
        Assert.Equal(expected, DeviceProblems.Classify(code));
    }

    [Fact]
    public void MissingDriverIsAWarningWithCodeInDetails()
    {
        var unknown = new DeviceInfo("PCI-Gerät", null, @"PCI\VEN_1AF4&DEV_1002", 28);

        var findings = DriverEvaluator.Evaluate([Wifi, unknown], [WifiDriver], isVm: false);

        var problem = Assert.Single(Problems(findings));
        Assert.Equal("PCI-Gerät", problem.Title);
        Assert.Equal(Severity.Warning, problem.Severity);
        Assert.Equal(DriverTexts.Problem_NoDriver, problem.Explanation);
        Assert.Contains("Code 28", problem.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void InAVirtualMachineTheGuestDriversAreMentioned()
    {
        var findings = DriverEvaluator.Evaluate([new DeviceInfo("PCI-Gerät", null, @"PCI\VEN_1AF4", 28)], [], isVm: true);

        Assert.Contains(DriverTexts.Problem_NoDriverVm, Assert.Single(Problems(findings)).Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledDevicesAreOnlyInfoAndDoNotCountAsProblems()
    {
        var findings = DriverEvaluator.Evaluate([Wifi with { ErrorCode = 22 }], [], isVm: false);

        var disabled = Assert.Single(findings, finding => finding.Category == DriverTexts.Category_Disabled);
        Assert.Equal(Severity.Info, disabled.Severity);
        Assert.Equal(Severity.Ok, Assert.Single(Problems(findings)).Severity);
    }

    [Fact]
    public void RestartNeededIsOnlyInfo()
    {
        var findings = DriverEvaluator.Evaluate([Wifi with { ErrorCode = 14 }], [], isVm: false);

        Assert.Equal(Severity.Info, Assert.Single(Problems(findings)).Severity);
    }

    [Fact]
    public void DeviceWithoutNameShowsItsId()
    {
        var findings = DriverEvaluator.Evaluate([new DeviceInfo("?", null, @"USB\VID_1234", 28)], [], isVm: false);

        Assert.Equal(@"USB\VID_1234", Assert.Single(Problems(findings)).Title);
    }

    [Theory]
    [InlineData(false, Severity.Warning)]
    [InlineData(true, Severity.Info)]
    public void BasicDisplayDriverIsAWarningOnRealPcs(bool isVm, Severity expected)
    {
        var basic = new DriverInfo(@"PCI\VEN_1234&DEV_1111", "DISPLAY", "Microsoft Basic Display Adapter", "Microsoft", "10.0.22621.1", new DateOnly(2006, 6, 21), "display.inf");

        var findings = DriverEvaluator.Evaluate([], [basic], isVm);

        // Die grüne Zeile bleibt: Kein Gerät meldet einen Fehler, die Grafik ist ein eigener Hinweis
        Assert.Equal([Severity.Ok, expected], Problems(findings).Select(finding => finding.Severity));
        Assert.Equal(DriverTexts.BasicDisplay_Title, Problems(findings).Last().Title);
    }

    [Fact]
    public void ImportantDriversAreExpertOnlyAndSkipSoftwareDevices()
    {
        var gpu = new DriverInfo(@"PCI\VEN_1002&DEV_1681", "DISPLAY", "AMD Radeon(TM) 680M", "Advanced Micro Devices, Inc.", "31.0.21921.1000", new DateOnly(2025, 1, 20), "oem7.inf");
        var vpn = new DriverInfo(@"ROOT\NET\0000", "NET", "WireGuard Tunnel", "WireGuard LLC", "0.5.3", null, "oem9.inf");
        var mouse = new DriverInfo(@"HID\VID_046D", "MOUSE", "HID-konforme Maus", "Microsoft", "10.0.1", null, "msmouse.inf");

        var findings = DriverEvaluator.Evaluate([Wifi], [WifiDriver, vpn, mouse, gpu], isVm: false);

        var drivers = findings.Where(finding => finding.Category == DriverTexts.Category_Drivers).ToList();
        Assert.Equal(["AMD Radeon(TM) 680M", "Intel(R) Wi-Fi 6 AX201"], drivers.Select(driver => driver.Title));
        Assert.All(drivers, driver => Assert.True(driver.IsExpertOnly && driver.Severity == Severity.Ok));
        Assert.Equal(Severity.Ok, new ModuleReport("drivers", findings).OverallSeverity);   // ohne Geräteprobleme grün, die Liste färbt nichts
        Assert.Contains("23.60.1.2", drivers[1].Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("20250709000000.******+000", 2025, 7, 9)]
    [InlineData("20060621000000.******+000", 2006, 6, 21)]
    public void WmiDatesAreParsed(string value, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), WindowsDeviceSource.ParseDate(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kaputt!!")]
    public void BadWmiDatesAreIgnored(string? value)
    {
        Assert.Null(WindowsDeviceSource.ParseDate(value));
    }
}
