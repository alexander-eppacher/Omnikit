using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Bios.Evaluation;
using Omnikit.Modules.Bios.Resources;
using Omnikit.Modules.Bios.Sources;

namespace Omnikit.Modules.Bios;

/// <summary>
/// BIOS-Modul: zeigt Version, Startmodus und Secure Boot und verlinkt die Support-Seite des Herstellers.
/// Nur lesen und melden. Omnikit flasht nie ein BIOS – auch nicht in späteren Versionen ohne neue Entscheidung.
/// </summary>
public sealed class BiosModule : IModule
{
    public const string OpenSupportPageActionId = "open-support-page";

    private readonly IFirmwareSource _firmware;
    private readonly TimeProvider _time;
    private readonly ILogger<BiosModule> _logger;

    // Stand der letzten Prüfung, für GetActions/ExecuteAsync
    private SupportPage? _support;

    public BiosModule(IFirmwareSource firmware, TimeProvider time, ILogger<BiosModule> logger)
    {
        _firmware = firmware;
        _time = time;
        _logger = logger;
    }

    public string Id => "bios";

    public string DisplayName => BiosTexts.ModuleName;

    // Läuft überall; ohne Windows steht auf der Seite „nicht verfügbar“.
    public bool IsSupported(SystemContext context) => true;

    public async Task<ModuleReport> CheckAsync(SystemContext context, CancellationToken cancellationToken)
    {
        _support = null;
        if (!context.IsWindows)
        {
            return new ModuleReport(Id, [NotAvailable(BiosTexts.Common_NotWindows)]);
        }

        var firmware = await Task.Run(_firmware.Read, cancellationToken);
        if (firmware is null)
        {
            return new ModuleReport(Id, [NotAvailable(BiosTexts.Common_ReadFailed)]);
        }

        var isVm = context.IsVirtualMachine == true;
        _support = isVm ? null : SupportPages.Find(context.Manufacturer, context.Model, firmware.BoardManufacturer);
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        return new ModuleReport(Id, FirmwareEvaluator.Evaluate(firmware, _support, isVm, today));
    }

    public IReadOnlyList<ModuleAction> GetActions(ModuleReport report) =>
        _support is null
            ? []
            : [new ModuleAction(
                OpenSupportPageActionId,
                string.Format(CultureInfo.CurrentCulture, BiosTexts.Action_OpenSupport, _support.Name),
                string.Format(CultureInfo.CurrentCulture, BiosTexts.Action_Confirm, _support.Name, _support.Url))];

    public Task<ActionResult> ExecuteAsync(ModuleAction action, CancellationToken cancellationToken)
    {
        if (action.Id != OpenSupportPageActionId || _support is null)
        {
            return Task.FromResult(new ActionResult(false, BiosTexts.Action_Unknown));
        }

        try
        {
            // Feste Adresse aus SupportPages, nichts aus Eingaben; öffnet den Standardbrowser
            using var process = Process.Start(new ProcessStartInfo(_support.Url.ToString()) { UseShellExecute = true });
            return Task.FromResult(new ActionResult(true, BiosTexts.Action_Opened));
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "Support-Seite konnte nicht geöffnet werden");
            return Task.FromResult(new ActionResult(false, BiosTexts.Action_Failed));
        }
    }

    private static Finding NotAvailable(string explanation) =>
        new(BiosTexts.Common_NotAvailableTitle, explanation, Severity.Info, BiosTexts.Category_Firmware);
}
