using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Drivers.Evaluation;
using Omnikit.Modules.Drivers.Resources;
using Omnikit.Modules.Drivers.Sources;

namespace Omnikit.Modules.Drivers;

/// <summary>
/// Treiber-Modul: findet Geräte, die Windows mit einem Fehler meldet (im Geräte-Manager mit gelbem Ausrufezeichen).
/// Installiert und ändert keine Treiber (keine Downloads von Drittseiten). Die Aktionen öffnen nur Windows-Fenster:
/// „Optionale Updates“ in den Einstellungen (dort wählt der Nutzer selbst) und den Geräte-Manager.
/// </summary>
public sealed class DriversModule : IModule
{
    public const string OpenOptionalUpdatesActionId = "open-optional-updates";
    public const string OpenDeviceManagerActionId = "open-device-manager";

    // Einstellungen › Windows Update › Erweiterte Optionen › Optionale Updates (dort stehen Treiber von Microsoft und den Herstellern)
    private const string OptionalUpdatesUri = "ms-settings:windowsupdate-optionalupdates";

    private readonly IDeviceSource _devices;
    private readonly ILogger<DriversModule> _logger;

    public DriversModule(IDeviceSource devices, ILogger<DriversModule> logger)
    {
        _devices = devices;
        _logger = logger;
    }

    public string Id => "drivers";

    public string DisplayName => DriverTexts.ModuleName;

    // Läuft überall; ohne Windows steht auf der Seite „nicht verfügbar“.
    public bool IsSupported(SystemContext context) => true;

    public async Task<ModuleReport> CheckAsync(SystemContext context, CancellationToken cancellationToken)
    {
        if (!context.IsWindows)
        {
            return new ModuleReport(Id, [NotAvailable(DriverTexts.Common_NotWindows)]);
        }

        var (devices, drivers) = await Task.Run(() => (_devices.ReadDevices(), _devices.ReadDrivers()), cancellationToken);
        if (devices is null)
        {
            return new ModuleReport(Id, [NotAvailable(DriverTexts.Common_ReadFailed)]);
        }

        return new ModuleReport(Id, DriverEvaluator.Evaluate(devices, drivers, context.IsVirtualMachine == true));
    }

    public IReadOnlyList<ModuleAction> GetActions(ModuleReport report)
    {
        if (report.Findings.Any(finding => finding.Title == DriverTexts.Common_NotAvailableTitle))
        {
            return []; // nicht unter Windows oder Liste nicht lesbar
        }

        var actions = new List<ModuleAction>();

        // Optionale Updates nur, wenn es etwas zu beheben gibt
        if (report.Findings.Any(finding => finding.Category == DriverTexts.Category_Problems && finding.Severity >= Severity.Warning))
        {
            actions.Add(new ModuleAction(OpenOptionalUpdatesActionId, DriverTexts.Action_OpenOptionalUpdates, DriverTexts.Action_ConfirmOptionalUpdates));
        }

        actions.Add(new ModuleAction(OpenDeviceManagerActionId, DriverTexts.Action_OpenDeviceManager, DriverTexts.Action_ConfirmDeviceManager));
        return actions;
    }

    public Task<ActionResult> ExecuteAsync(ModuleAction action, CancellationToken cancellationToken)
    {
        // Beide öffnen nur ein Windows-Fenster mit normalen Rechten; ändern kann der Nutzer dort selbst (Windows fragt dann nach Adminrechten)
        var (target, opened) = action.Id switch
        {
            OpenOptionalUpdatesActionId => (OptionalUpdatesUri, DriverTexts.Action_OpenedOptionalUpdates),
            OpenDeviceManagerActionId => ("devmgmt.msc", DriverTexts.Action_OpenedDeviceManager),
            _ => (null, null),
        };

        if (target is null || opened is null)
        {
            return Task.FromResult(new ActionResult(false, DriverTexts.Action_Unknown));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new ActionResult(false, DriverTexts.Common_NotWindows));
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return Task.FromResult(new ActionResult(true, opened));
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "{Target} konnte nicht geöffnet werden", target);
            return Task.FromResult(new ActionResult(false, DriverTexts.Action_Failed));
        }
    }

    private static Finding NotAvailable(string explanation) =>
        new(DriverTexts.Common_NotAvailableTitle, explanation, Severity.Info, DriverTexts.Category_Problems);
}
