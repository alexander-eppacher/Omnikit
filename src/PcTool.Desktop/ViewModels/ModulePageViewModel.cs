using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcTool.Core.Modules;
using PcTool.Core.Reports;
using PcTool.Core.SystemInfo;
using PcTool.Desktop.Resources;
using PcTool.Desktop.Services;

namespace PcTool.Desktop.ViewModels;

/// <summary>
/// Seite für ein beliebiges Modul: Gesamtampel, Befunde nach Bereich, Aktionen, Export.
/// Neue Module bekommen diese Seite automatisch, ohne eigenen UI-Code.
/// </summary>
public partial class ModulePageViewModel : ViewModelBase
{
    private readonly IModule _module;
    private readonly SystemContext _context;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private ModuleReport? _report;

    public ModulePageViewModel(IModule module, SystemContext context, IDialogService dialogs, TimeProvider time, ILogger logger)
    {
        _module = module;
        _context = context;
        _dialogs = dialogs;
        _time = time;
        _logger = logger;

        // Erste Prüfung startet sofort im Hintergrund.
        RefreshCommand.Execute(null);
    }

    public string Title => _module.DisplayName;

    public ObservableCollection<FindingGroupViewModel> Groups { get; } = [];

    public ObservableCollection<ActionViewModel> Actions { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(ExportCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial Severity? OverallSeverity { get; set; }

    /// <summary>Meldung nach einer Aktion oder einem Export (z. B. "Bericht gespeichert").</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    // Die Aktionsknöpfe werden zur Laufzeit erzeugt und müssen deshalb selbst erfahren, wann die Seite beschäftigt ist.
    partial void OnIsBusyChanged(bool value)
    {
        foreach (var action in Actions)
        {
            action.Command.NotifyCanExecuteChanged();
        }
    }

    private bool CanRun => !IsBusy;

    private bool CanExport => !IsBusy && _report is not null;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            _report = await _module.CheckAsync(_context, CancellationToken.None);
            ShowReport(_report);
        }
#pragma warning disable CA1031 // Ein Fehler in einem Modul soll verständlich angezeigt werden, nicht die App beenden.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Prüfung von Modul {ModuleId} fehlgeschlagen", _module.Id);
            StatusMessage = Strings.ModulePage_CheckFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (_report is null)
        {
            return;
        }

        try
        {
            var now = _time.GetLocalNow();
            await using var stream = await _dialogs.PickSaveFileAsync(ReportExporter.SuggestFileName(now));
            if (stream is null)
            {
                return; // Nutzer hat abgebrochen
            }

            await ReportExporter.WriteAsync(stream, _context, [_report], now, CancellationToken.None);
            StatusMessage = Strings.ModulePage_ExportDone;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Bericht konnte nicht gespeichert werden");
            StatusMessage = Strings.ModulePage_ExportFailed;
        }
    }

    /// <summary>
    /// Führt eine Aktion aus, aber IMMER erst nach Bestätigung durch den Nutzer (Regel 6.1.1).
    /// </summary>
    private async Task ExecuteActionAsync(ModuleAction action)
    {
        var confirmed = await _dialogs.ConfirmAsync(action.ConfirmationTitle ?? action.DisplayName, action.ConfirmationText);
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _module.ExecuteAsync(action, CancellationToken.None);
            StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync();
    }

    private void ShowReport(ModuleReport report)
    {
        OverallSeverity = report.OverallSeverity;

        Groups.Clear();
        foreach (var group in report.Findings.GroupBy(finding => finding.Category ?? string.Empty))
        {
            Groups.Add(new FindingGroupViewModel(group.Key, group.ToList()));
        }

        Actions.Clear();
        foreach (var action in _module.GetActions(report))
        {
            Actions.Add(new ActionViewModel(action.DisplayName, new AsyncRelayCommand(() => ExecuteActionAsync(action), () => !IsBusy)));
        }
    }
}

/// <summary>Ein Bereich auf der Modulseite, z. B. "Laufwerke" mit seinen Befunden.</summary>
public sealed record FindingGroupViewModel(string Title, IReadOnlyList<Finding> Findings)
{
    public bool HasTitle => !string.IsNullOrEmpty(Title);
}

/// <summary>Eine Schaltfläche für eine Modul-Aktion.</summary>
public sealed record ActionViewModel(string DisplayName, IAsyncRelayCommand Command);
