using System.Globalization;
using Omnikit.Core.Modules;
using Omnikit.Modules.Drivers.Data;
using Omnikit.Modules.Drivers.Resources;

namespace Omnikit.Modules.Drivers.Evaluation;

/// <summary>Macht aus Geräte- und Treiberliste die Befunde für die Seite „Treiber“.</summary>
public static class DriverEvaluator
{
    /// <summary>Wichtige Geräteklassen für die Treiberliste der Expertenansicht, in dieser Reihenfolge.</summary>
    internal static readonly string[] ImportantClasses = ["DISPLAY", "NET", "MEDIA", "BLUETOOTH", "CAMERA", "IMAGE", "SCSIADAPTER"];

    // Notfalltreiber von Windows für Grafikkarten („Microsoft Basic Display Adapter“)
    private const string BasicDisplayInf = "display.inf";

    public static IReadOnlyList<Finding> Evaluate(IReadOnlyList<DeviceInfo> devices, IReadOnlyList<DriverInfo> drivers, bool isVm)
    {
        var findings = new List<Finding>();

        foreach (var device in devices.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var problem = DeviceProblems.Classify(device.ErrorCode);
            if (problem is not DeviceProblem.None)
            {
                findings.Add(ForProblem(device, problem, isVm));
            }
        }

        // Grüne Zeile, wenn kein Gerät einen Fehler meldet (ausgeschaltete Geräte sind kein Fehler)
        if (!findings.Any(finding => finding.Category == DriverTexts.Category_Problems))
        {
            findings.Add(new Finding(
                DriverTexts.NoProblems_Title, Format(DriverTexts.NoProblems_Text, devices.Count), Severity.Ok, DriverTexts.Category_Problems));
        }

        findings.AddRange(drivers
            .Where(driver => IsDisplay(driver) && string.Equals(driver.InfName, BasicDisplayInf, StringComparison.OrdinalIgnoreCase))
            .Select(driver => new Finding(
                DriverTexts.BasicDisplay_Title,
                isVm ? DriverTexts.BasicDisplay_Vm : DriverTexts.BasicDisplay_Text,
                isVm ? Severity.Info : Severity.Warning,
                DriverTexts.Category_Problems,
                Format(DriverTexts.Details_Driver, driver.InfName ?? "?", driver.DeviceId))));

        findings.AddRange(ImportantDrivers(drivers));
        return findings;
    }

    /// <summary>Software-Geräte (z. B. VPN-Adapter, Lautsprecher-Endpunkte) haben keine eigene Hardware.</summary>
    internal static bool IsHardware(string deviceId) =>
        !deviceId.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase)
        && !deviceId.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase);

    private static Finding ForProblem(DeviceInfo device, DeviceProblem problem, bool isVm)
    {
        var name = device.Name is "?" or "" ? device.DeviceId : device.Name;
        var details = Format(DriverTexts.Details_Problem, device.ErrorCode, device.DeviceClass ?? "?", device.DeviceId);

        if (problem == DeviceProblem.Disabled)
        {
            return new Finding(name, DriverTexts.Problem_Disabled, Severity.Info, DriverTexts.Category_Disabled, details);
        }

        var text = problem switch
        {
            DeviceProblem.NoDriver => isVm ? DriverTexts.Problem_NoDriver + " " + DriverTexts.Problem_NoDriverVm : DriverTexts.Problem_NoDriver,
            DeviceProblem.DriverBroken => DriverTexts.Problem_DriverBroken,
            DeviceProblem.DriverBlocked => DriverTexts.Problem_DriverBlocked,
            DeviceProblem.NotStarting => DriverTexts.Problem_NotStarting,
            DeviceProblem.Resources => DriverTexts.Problem_Resources,
            DeviceProblem.RestartNeeded => DriverTexts.Problem_RestartNeeded,
            _ => Format(DriverTexts.Problem_Other, device.ErrorCode),
        };

        // Ein Neustart behebt es meist von selbst: nur ein Hinweis, keine Warnung
        var severity = problem == DeviceProblem.RestartNeeded ? Severity.Info : Severity.Warning;
        return new Finding(name, text, severity, DriverTexts.Category_Problems, details);
    }

    /// <summary>Treiber von Grafik, Netzwerk, Audio usw. – nur in der Expertenansicht.</summary>
    private static IEnumerable<Finding> ImportantDrivers(IReadOnlyList<DriverInfo> drivers) =>
        drivers
            .Where(driver => IsHardware(driver.DeviceId) && ClassOrder(driver) >= 0)
            .OrderBy(ClassOrder)
            .ThenBy(driver => driver.DeviceName, StringComparer.CurrentCultureIgnoreCase)
            .Select(driver => new Finding(
                driver.DeviceName ?? driver.DeviceId,
                Format(DriverTexts.Driver_Text,
                    driver.Provider ?? "?",
                    driver.Version ?? "?",
                    driver.Date?.ToString("d", CultureInfo.CurrentCulture) ?? "?"),
                Severity.Info,
                DriverTexts.Category_Drivers,
                Format(DriverTexts.Details_Driver, driver.InfName ?? "?", driver.DeviceId),
                IsExpertOnly: true));

    private static int ClassOrder(DriverInfo driver) =>
        Array.FindIndex(ImportantClasses, name => string.Equals(name, driver.DeviceClass, StringComparison.OrdinalIgnoreCase));

    private static bool IsDisplay(DriverInfo driver) => string.Equals(driver.DeviceClass, "DISPLAY", StringComparison.OrdinalIgnoreCase);

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
