using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Elevation;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Evaluation;
using Omnikit.Modules.Health.Resources;
using Omnikit.Modules.Health.Sources;

namespace Omnikit.Modules.Health;

/// <summary>
/// Health-Modul: prüft Laufwerke, Speicherplatz, Akku und Windows-Zustand.
/// Die Auslastung (CPU, RAM …) steht auf der eigenen Seite „Leistung“ (Omnikit.Modules.Performance).
/// Ändert nichts am System. Einzige Aktion: Laufwerksdetails mit Adminrechten nachladen.
/// </summary>
public sealed class HealthModule : IModule
{
    public const string LoadDiskDetailsActionId = "load-disk-details";

    private readonly IDiskSource _disks;
    private readonly IDiskReliabilitySource _reliability;
    private readonly IVolumeSource _volumes;
    private readonly IBatterySource _battery;
    private readonly IWindowsStateSource _windowsState;
    private readonly IUpdateSource _updates;
    private readonly IElevationRunner _elevation;
    private readonly TimeProvider _time;
    private readonly ILogger<HealthModule> _logger;

    // Ergebnis aus dem Hilfsprozess; bleibt erhalten, bis die App beendet wird.
    private IReadOnlyList<DiskReliability>? _elevatedReliability;

    // Stand der letzten Prüfung, für GetActions.
    private bool _diskDetailsNeedAdmin;
    private bool? _isAdminAccount;

    public HealthModule(
        IDiskSource disks,
        IDiskReliabilitySource reliability,
        IVolumeSource volumes,
        IBatterySource battery,
        IWindowsStateSource windowsState,
        IUpdateSource updates,
        IElevationRunner elevation,
        TimeProvider time,
        ILogger<HealthModule> logger)
    {
        _disks = disks;
        _reliability = reliability;
        _volumes = volumes;
        _battery = battery;
        _windowsState = windowsState;
        _updates = updates;
        _elevation = elevation;
        _time = time;
        _logger = logger;
    }

    public string Id => "health";

    public string DisplayName => HealthTexts.ModuleName;

    // Läuft überall; was es nur unter Windows gibt, wird als "nicht verfügbar" angezeigt.
    public bool IsSupported(SystemContext context) => true;

    public async Task<ModuleReport> CheckAsync(SystemContext context, CancellationToken cancellationToken)
    {
        _isAdminAccount = context.IsAdminAccount;
        var isVm = context.IsVirtualMachine == true;

        // Die Update-Suche dauert am längsten, deshalb zuerst starten und parallel weitermachen.
        var updatesTask = context.IsWindows ? _updates.CheckAsync(cancellationToken) : Task.FromResult<UpdateStatus?>(null);

        var findings = new List<Finding>();
        findings.AddRange(await Task.Run(() => CheckDisks(context, isVm), cancellationToken));
        findings.AddRange(await Task.Run(CheckVolumes, cancellationToken));
        findings.AddRange(await CheckBatteryAsync(context, isVm, cancellationToken));

        if (context.IsWindows)
        {
            findings.AddRange(WindowsStateEvaluator.Evaluate(await Task.Run(_windowsState.Read, cancellationToken), _time.GetLocalNow()));
            findings.Add(WindowsStateEvaluator.EvaluateUpdates(await updatesTask));
        }
        else
        {
            findings.Add(NotAvailable(HealthTexts.Category_Windows, HealthTexts.Common_NotWindows));
        }

        return new ModuleReport(Id, findings);
    }

    public IReadOnlyList<ModuleAction> GetActions(ModuleReport report)
    {
        if (!_diskDetailsNeedAdmin)
        {
            return [];
        }

        // Der Hinweistext hängt davon ab, ob Windows nur "Ja/Nein" oder ein Admin-Passwort verlangen wird.
        var confirmationText = _isAdminAccount switch
        {
            true => HealthTexts.Action_Confirm_AdminAccount,
            false => HealthTexts.Action_Confirm_StandardAccount,
            null => HealthTexts.Action_Confirm_UnknownAccount,
        };

        return [new ModuleAction(LoadDiskDetailsActionId, HealthTexts.Action_LoadDiskDetails, confirmationText, HealthTexts.Action_Confirm_Title)];
    }

    public async Task<ActionResult> ExecuteAsync(ModuleAction action, CancellationToken cancellationToken)
    {
        if (action.Id != LoadDiskDetailsActionId)
        {
            return new ActionResult(false, HealthTexts.Action_Unknown);
        }

        var result = await _elevation.RunAsync(DiskReliabilityOperation.OperationId, cancellationToken);

        switch (result.Status)
        {
            case ElevationStatus.Success when result.Payload is not null:
                try
                {
                    _elevatedReliability = DiskReliabilityOperation.Deserialize(result.Payload);
                    return new ActionResult(true, HealthTexts.Action_Success);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Antwort mit Laufwerksdetails ist ungültig");
                    return new ActionResult(false, HealthTexts.Action_Failed);
                }

            case ElevationStatus.Cancelled:
                return new ActionResult(false, HealthTexts.Action_Cancelled);

            case ElevationStatus.NotSupported:
                return new ActionResult(false, HealthTexts.Common_NotWindows);

            default:
                return new ActionResult(false, HealthTexts.Action_Failed);
        }
    }

    private List<Finding> CheckDisks(SystemContext context, bool isVm)
    {
        _diskDetailsNeedAdmin = false;
        var category = HealthTexts.Category_Disks;

        if (!context.IsWindows)
        {
            return [NotAvailable(category, HealthTexts.Common_NotWindows)];
        }

        if (isVm)
        {
            return [NotAvailable(category, HealthTexts.Disk_Vm)];
        }

        var disks = _disks.ReadDisks();
        if (disks.Count == 0)
        {
            return [NotAvailable(category, HealthTexts.Disk_ReadFailed)];
        }

        var reliability = ReadReliability(context);
        var findings = disks
            .SelectMany(disk => DiskEvaluator.Evaluate(disk, reliability?.FirstOrDefault(r => r.DeviceId == disk.DeviceId)))
            .ToList();

        if (_diskDetailsNeedAdmin)
        {
            findings.Add(new Finding(HealthTexts.Disk_DetailsNeedAdmin_Title, HealthTexts.Disk_DetailsNeedAdmin_Text, Severity.Info, category));
        }

        return findings;
    }

    /// <summary>
    /// Versucht zuerst, die Details ohne Adminrechte zu lesen (klappt auf manchen Geräten).
    /// Klappt das nicht, werden die Werte aus dem Hilfsprozess verwendet, falls schon geladen.
    /// </summary>
    private IReadOnlyList<DiskReliability>? ReadReliability(SystemContext context)
    {
        try
        {
            var values = _reliability.Read();

            // Manche Windows-Versionen liefern ohne Adminrechte einfach nichts statt einer Fehlermeldung.
            if (values.Count > 0 || context.IsAdmin)
            {
                return values;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // erwartet ohne Adminrechte, siehe unten
        }
#pragma warning disable CA1031 // Ein Fehler bei den Details soll die restliche Prüfung nicht verhindern.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Laufwerksdetails konnten nicht gelesen werden");
            return null;
        }

        _diskDetailsNeedAdmin = _elevatedReliability is null;
        return _elevatedReliability;
    }

    private List<Finding> CheckVolumes() =>
        _volumes.ReadVolumes().Select(StorageEvaluator.Evaluate).ToList();

    private async Task<List<Finding>> CheckBatteryAsync(SystemContext context, bool isVm, CancellationToken cancellationToken)
    {
        var category = HealthTexts.Category_Battery;

        if (isVm)
        {
            return [NotAvailable(category, HealthTexts.Battery_Vm)];
        }

        // Desktop-PC ohne Akku: Bereich gar nicht anzeigen.
        if (context.IsLaptop != true)
        {
            return [];
        }

        var batteries = await _battery.ReadAsync(cancellationToken);
        if (batteries is null || batteries.Count == 0)
        {
            return [NotAvailable(category, HealthTexts.Battery_ReadFailed)];
        }

        return batteries.SelectMany(BatteryEvaluator.Evaluate).ToList();
    }

    private static Finding NotAvailable(string category, string explanation) =>
        new(HealthTexts.Common_NotAvailableTitle, explanation, Severity.Info, category);
}
