using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Desktop.Services;
using Omnikit.Modules.Apps.Catalog;
using Omnikit.Modules.Apps.Winget;
using Texts = Omnikit.Modules.Apps.Resources.AppsTexts;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Seite „Apps“: Updates installierter Programme und Katalog zum Installieren.
/// Lesen (Listen) passiert beim ersten Öffnen der Seite; Installieren und Aktualisieren nur nach Bestätigung (Regel 6.1.1).
/// Die Programme laufen nacheinander, weil winget nicht mehrere Installationen gleichzeitig verträgt.
/// </summary>
public sealed partial class AppsPageViewModel : ViewModelBase, IActivatablePage
{
    // Aktualisiert winget sich selbst (App-Installer), wäre es für die folgenden Programme kurz weg: deshalb immer zuletzt.
    private const string AppInstallerId = "Microsoft.AppInstaller";

    private readonly IWingetClient _winget;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private readonly Dictionary<string, CatalogItemViewModel> _catalogItems = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, InstalledPackage> _installed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deselectedUpgrades = new(StringComparer.OrdinalIgnoreCase);
    private CatalogGroupViewModel? _templateExtras;
    private bool _loadedOnce;
    private bool _stopRequested;

    public AppsPageViewModel(IWingetClient winget, IDialogService dialogs, ViewModeState viewMode, SystemContext context, ILogger logger)
    {
        _winget = winget;
        _dialogs = dialogs;
        _logger = logger;
        ViewMode = viewMode;
        IsSupported = context.IsWindows;

        foreach (var group in BuiltInCatalog.Apps.GroupBy(app => app.Category).OrderBy(group => group.Key))
        {
            var items = group.Select(CreateItem).ToList();
            CatalogGroups.Add(new CatalogGroupViewModel(BuiltInCatalog.CategoryName(group.Key), items));
        }
    }

    public bool IsSupported { get; }

    /// <summary>Für die Expertenansicht (Paket-IDs, Quellen, Fehlercodes).</summary>
    public ViewModeState ViewMode { get; }

    public IReadOnlyList<AppTemplate> Templates { get; } = BuiltInCatalog.Templates;

    public ObservableCollection<CatalogGroupViewModel> CatalogGroups { get; } = [];

    public ObservableCollection<UpgradeItemViewModel> Upgrades { get; } = [];

    public ObservableCollection<JobViewModel> Jobs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(InstallSelectedCommand), nameof(UpgradeSelectedCommand))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(InstallSelectedCommand), nameof(UpgradeSelectedCommand), nameof(StopCommand))]
    public partial bool IsRunning { get; set; }

    /// <summary>Listen erfolgreich gelesen: erst dann sind Updates und Katalog sichtbar.</summary>
    [ObservableProperty]
    public partial bool IsReady { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Kurze Rückmeldung, z. B. „Vorlage geladen“ oder die Zusammenfassung nach einer Installation.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? UpgradesSummary { get; set; }

    [ObservableProperty]
    public partial string? SelectedSummary { get; set; }

    [ObservableProperty]
    public partial bool IsStopping { get; set; }

    /// <summary>Immer sichtbare Zeile oben während der Arbeit, z. B. „Programm 2 von 5: VLC media player“.</summary>
    [ObservableProperty]
    public partial string? CurrentJobText { get; set; }

    public bool IsBusy => IsLoading || IsRunning;

    public bool HasJobs => Jobs.Count > 0;

    public bool HasUpgrades => Upgrades.Count > 0;

    private bool CanStart => !IsBusy;

    public void Activate()
    {
        // Die Listen nur beim ersten Öffnen lesen; danach über „Erneut suchen“.
        if (IsSupported && !_loadedOnce)
        {
            _loadedOnce = true;
            RefreshCommand.Execute(null);
        }
    }

    public void Deactivate()
    {
        // Laufende Installationen gehen weiter, auch wenn eine andere Seite geöffnet wird.
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var installed = await _winget.GetInstalledAsync(CancellationToken.None);
            var upgrades = await _winget.GetUpgradesAsync(installed, CancellationToken.None);
            ShowInstalled(installed);
            ShowUpgrades(upgrades);
            IsReady = true;
        }
        catch (WingetMissingException ex)
        {
            _logger.LogWarning(ex, "winget nicht gefunden");
            ErrorMessage = Texts.WingetMissing;
            IsReady = false;
        }
        catch (WingetException ex)
        {
            ErrorMessage = ex.Outcome == WingetOutcome.NoNetwork ? Texts.LoadFailed_NoNetwork : Texts.LoadFailed;
        }
#pragma warning disable CA1031 // Ein unerwarteter Fehler soll verständlich angezeigt werden, nicht die App beenden.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Programmliste konnte nicht gelesen werden");
            ErrorMessage = Texts.LoadFailed;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task InstallSelectedAsync()
    {
        var items = AllCatalogItems().Where(item => item.IsSelected && !item.IsInstalled).ToList();
        if (items.Count == 0)
        {
            return;
        }

        var list = string.Join('\n', items.Select(item => "• " + item.Name));
        if (!await _dialogs.ConfirmAsync(Texts.Confirm_InstallTitle, Format(Texts.Confirm_Install, list)))
        {
            return;
        }

        await RunAsync(items.Select(item => (item.PackageId, item.Name)).ToList(), _winget.InstallAsync);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task UpgradeSelectedAsync()
    {
        var items = Upgrades.Where(item => item.IsSelected)
            .OrderBy(item => string.Equals(item.PackageId, AppInstallerId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (items.Count == 0)
        {
            return;
        }

        var list = string.Join('\n', items.Select(item => $"• {item.Name} ({item.VersionText})"));
        if (!await _dialogs.ConfirmAsync(Texts.Confirm_UpgradeTitle, Format(Texts.Confirm_Upgrade, list)))
        {
            return;
        }

        await RunAsync(items.Select(item => (item.PackageId, item.Name)).ToList(), _winget.UpgradeAsync);
    }

    [RelayCommand]
    private void SelectAllUpgrades()
    {
        var select = Upgrades.Any(item => !item.IsSelected);
        foreach (var item in Upgrades)
        {
            item.IsSelected = select;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop()
    {
        _stopRequested = true;
        IsStopping = true;
    }

    [RelayCommand]
    private void ApplyTemplate(AppTemplate template) => SelectTemplate(template);

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in AllCatalogItems())
        {
            item.IsSelected = false;
        }

        RemoveTemplateExtras();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task LoadTemplateAsync()
    {
        try
        {
            await using var stream = await _dialogs.PickOpenFileAsync();
            if (stream is null)
            {
                return; // abgebrochen
            }

            var template = await TemplateFile.LoadAsync(stream, Texts.Template_DefaultName, CancellationToken.None);
            if (template is null)
            {
                StatusMessage = Texts.Template_LoadFailed;
                return;
            }

            SelectTemplate(template);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Vorlage konnte nicht gelesen werden");
            StatusMessage = Texts.Template_LoadFailed;
        }
    }

    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        var ids = AllCatalogItems().Where(item => item.IsSelected).Select(item => item.PackageId).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            await using var stream = await _dialogs.PickSaveFileAsync(Texts.Template_FileName);
            if (stream is null)
            {
                return;
            }

            await TemplateFile.SaveAsync(stream, new AppTemplate(Texts.Template_DefaultName, ids), CancellationToken.None);
            StatusMessage = Texts.Template_Saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Vorlage konnte nicht gespeichert werden");
            StatusMessage = Texts.Template_SaveFailed;
        }
    }

    /// <summary>Führt die Programme nacheinander aus und zeigt den Fortschritt. Danach werden die Listen neu gelesen.</summary>
    private async Task RunAsync(
        IReadOnlyList<(string PackageId, string Name)> packages, Func<string, CancellationToken, Task<PackageResult>> action)
    {
        Jobs.Clear();
        foreach (var (packageId, name) in packages)
        {
            Jobs.Add(new JobViewModel(packageId, name, Texts.Job_Waiting));
        }

        OnPropertyChanged(nameof(HasJobs));
        StatusMessage = null;
        _stopRequested = false;
        IsRunning = true;
        var succeeded = 0;
        try
        {
            foreach (var (job, index) in Jobs.Select((job, index) => (job, index)))
            {
                CurrentJobText = Format(Texts.Jobs_Current, index + 1, Jobs.Count, job.Name);
                if (_stopRequested)
                {
                    job.StateText = Texts.Job_Skipped;
                    job.Severity = Severity.Warning;
                    continue;
                }

                job.IsRunning = true;
                job.StateText = Texts.Job_Running;
                var result = await RunOneAsync(job, action);
                job.IsRunning = false;
                job.StateText = OutcomeText.For(result.Outcome);
                job.Severity = result.IsSuccess ? Severity.Ok : Severity.Critical;
                job.CodeText = result.ExitCode != 0 ? Format(Texts.Expert_Code, WingetExitCodes.Format(result.ExitCode)) : null;
                succeeded += result.IsSuccess ? 1 : 0;
            }
        }
        finally
        {
            IsRunning = false;
            IsStopping = false;
            CurrentJobText = null;
        }

        StatusMessage = Format(Texts.Jobs_Summary, succeeded, Jobs.Count);
        await RefreshAsync();
    }

    private async Task<PackageResult> RunOneAsync(JobViewModel job, Func<string, CancellationToken, Task<PackageResult>> action)
    {
        try
        {
            return await action(job.PackageId, CancellationToken.None);
        }
        catch (WingetMissingException ex)
        {
            _logger.LogWarning(ex, "winget nicht gefunden");
            _stopRequested = true;
            ErrorMessage = Texts.WingetMissing;
            return new PackageResult(job.PackageId, WingetOutcome.Failed, 0);
        }
#pragma warning disable CA1031 // Scheitert ein Programm unerwartet, sollen die übrigen trotzdem laufen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Programm {PackageId} konnte nicht verarbeitet werden", job.PackageId);
            return new PackageResult(job.PackageId, WingetOutcome.Failed, 0);
        }
    }

    private void SelectTemplate(AppTemplate template)
    {
        ClearSelection();

        var unknown = new List<CatalogItemViewModel>();
        foreach (var id in template.PackageIds)
        {
            if (_catalogItems.TryGetValue(id, out var item))
            {
                item.IsSelected = true;
            }
            else
            {
                // Nicht im Katalog (z. B. aus einer selbst gespeicherten Vorlage): als eigener Bereich anzeigen, mit der ID als Name.
                var extra = new CatalogItemViewModel(id, id, Texts.Catalog_FromTemplateDescription) { IsSelected = true };
                extra.PropertyChanged += OnItemChanged;
                unknown.Add(extra);
            }
        }

        if (unknown.Count > 0)
        {
            _templateExtras = new CatalogGroupViewModel(Texts.Catalog_FromTemplate, unknown);
            CatalogGroups.Add(_templateExtras);
            MarkInstalled(unknown);
        }

        StatusMessage = Format(Texts.Template_Loaded, template.Name);
    }

    private void RemoveTemplateExtras()
    {
        if (_templateExtras is not null)
        {
            foreach (var item in _templateExtras.Items)
            {
                item.PropertyChanged -= OnItemChanged;
            }

            CatalogGroups.Remove(_templateExtras);
            _templateExtras = null;
            UpdateSelectedSummary();
        }
    }

    private void ShowInstalled(IReadOnlyList<InstalledPackage> installed)
    {
        var byId = new Dictionary<string, InstalledPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in installed)
        {
            byId[package.Id] = package;
        }

        _installed = byId;
        MarkInstalled(AllCatalogItems());
    }

    private void MarkInstalled(IEnumerable<CatalogItemViewModel> items)
    {
        foreach (var item in items)
        {
            var isInstalled = _installed.TryGetValue(item.PackageId, out var package);
            item.IsInstalled = isInstalled;
            item.InstalledVersion = package?.Version;
        }

        UpdateSelectedSummary();
    }

    private void ShowUpgrades(IReadOnlyList<AvailableUpgrade> upgrades)
    {
        // Was der Nutzer abgewählt hat, bleibt nach dem Neuladen abgewählt (z. B. ein Programm, das er nicht aktualisieren will).
        foreach (var item in Upgrades)
        {
            if (item.IsSelected)
            {
                _deselectedUpgrades.Remove(item.PackageId);
            }
            else
            {
                _deselectedUpgrades.Add(item.PackageId);
            }
        }

        Upgrades.Clear();
        foreach (var upgrade in upgrades.OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Upgrades.Add(new UpgradeItemViewModel(
                upgrade.Id, upgrade.Name, Format(Texts.Updates_Version, upgrade.InstalledVersion, upgrade.AvailableVersion), upgrade.Source)
            {
                IsSelected = !_deselectedUpgrades.Contains(upgrade.Id),
            });
        }

        OnPropertyChanged(nameof(HasUpgrades));
        UpgradesSummary = Upgrades.Count switch
        {
            0 => Texts.Updates_None,
            1 => Texts.Updates_One,
            _ => Format(Texts.Updates_Count, Upgrades.Count),
        };
    }

    private CatalogItemViewModel CreateItem(CatalogApp app)
    {
        var item = new CatalogItemViewModel(app.PackageId, app.Name, app.Description);
        item.PropertyChanged += OnItemChanged;
        _catalogItems[app.PackageId] = item;
        return item;
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CatalogItemViewModel.IsSelected))
        {
            UpdateSelectedSummary();
        }
    }

    private void UpdateSelectedSummary()
    {
        var count = AllCatalogItems().Count(item => item.IsSelected && !item.IsInstalled);
        SelectedSummary = count > 0 ? Format(Texts.Catalog_Selected, count) : null;
    }

    private IEnumerable<CatalogItemViewModel> AllCatalogItems() => CatalogGroups.SelectMany(group => group.Items);

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
