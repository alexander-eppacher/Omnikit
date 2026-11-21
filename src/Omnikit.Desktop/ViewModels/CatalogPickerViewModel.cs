using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Omnikit.Desktop.Services;
using Omnikit.Modules.Apps.Catalog;
using Omnikit.Modules.Apps.Winget;
using Texts = Omnikit.Modules.Apps.Resources.AppsTexts;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Programmkatalog mit Häkchen und Vorlagen. Wird auf der Seite „Apps“ (zum Installieren) und im
/// Stick-Builder (für die Programme nach der Neuinstallation) verwendet – dieselbe Liste, dieselbe Bedienung.
/// </summary>
public sealed partial class CatalogPickerViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private Dictionary<string, InstalledPackage> _installed = new(StringComparer.OrdinalIgnoreCase);
    private CatalogGroupViewModel? _templateExtras;

    public CatalogPickerViewModel(IDialogService dialogs, ViewModeState viewMode, ILogger logger)
    {
        _dialogs = dialogs;
        _logger = logger;
        ViewMode = viewMode;

        foreach (var group in BuiltInCatalog.Apps.GroupBy(app => app.Category).OrderBy(group => group.Key))
        {
            var items = group.Select(app => Track(new CatalogItemViewModel(app.PackageId, app.Name, app.Description))).ToList();
            Groups.Add(new CatalogGroupViewModel(BuiltInCatalog.CategoryName(group.Key), items));
        }
    }

    /// <summary>Für die Expertenansicht (Paket-IDs).</summary>
    public ViewModeState ViewMode { get; }

    public IReadOnlyList<AppTemplate> Templates { get; } = BuiltInCatalog.Templates;

    public ObservableCollection<CatalogGroupViewModel> Groups { get; } = [];

    /// <summary>Rückmeldung zu Vorlagen, z. B. „Vorlage „Büro“ geladen.“</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>z. B. „3 ausgewählt“ (nur Programme, die noch nicht installiert sind).</summary>
    [ObservableProperty]
    public partial string? SelectedSummary { get; set; }

    /// <summary>Wird bei jeder Änderung der Auswahl ausgelöst, z. B. damit eine Seite ihre Knöpfe aktualisiert.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Ausgewählt und noch nicht installiert.</summary>
    public IReadOnlyList<CatalogItemViewModel> SelectedToInstall() =>
        AllItems().Where(item => item.IsSelected && !item.IsInstalled).ToList();

    /// <summary>Alle Häkchen, auch bei bereits installierten Programmen (z. B. für eine Vorlage).</summary>
    public IReadOnlyList<string> SelectedIds() =>
        AllItems().Where(item => item.IsSelected).Select(item => item.PackageId).ToList();

    /// <summary>Markiert installierte Programme (nur Seite „Apps“).</summary>
    public void MarkInstalled(IEnumerable<InstalledPackage> installed)
    {
        var byId = new Dictionary<string, InstalledPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in installed)
        {
            byId[package.Id] = package;
        }

        _installed = byId;
        MarkInstalled(AllItems());
    }

    [RelayCommand]
    private void ApplyTemplate(AppTemplate template) => SelectTemplate(template);

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in AllItems())
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
        var ids = SelectedIds();
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

    private void SelectTemplate(AppTemplate template)
    {
        ClearSelection();

        var catalogItems = AllItems().ToDictionary(item => item.PackageId, StringComparer.OrdinalIgnoreCase);
        var unknown = new List<CatalogItemViewModel>();
        foreach (var id in template.PackageIds)
        {
            if (catalogItems.TryGetValue(id, out var item))
            {
                item.IsSelected = true;
            }
            else
            {
                // Nicht im Katalog (z. B. aus einer selbst gespeicherten Vorlage): als eigener Bereich, mit der ID als Name.
                unknown.Add(Track(new CatalogItemViewModel(id, id, Texts.Catalog_FromTemplateDescription) { IsSelected = true }));
            }
        }

        if (unknown.Count > 0)
        {
            _templateExtras = new CatalogGroupViewModel(Texts.Catalog_FromTemplate, unknown);
            Groups.Add(_templateExtras);
            MarkInstalled(unknown);
        }

        StatusMessage = string.Format(CultureInfo.CurrentCulture, Texts.Template_Loaded, template.Name);
    }

    private void RemoveTemplateExtras()
    {
        if (_templateExtras is null)
        {
            return;
        }

        foreach (var item in _templateExtras.Items)
        {
            item.PropertyChanged -= OnItemChanged;
        }

        Groups.Remove(_templateExtras);
        _templateExtras = null;
        UpdateSelectedSummary();
    }

    private void MarkInstalled(IEnumerable<CatalogItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.IsInstalled = _installed.TryGetValue(item.PackageId, out var package);
            item.InstalledVersion = package?.Version;
        }

        UpdateSelectedSummary();
    }

    private CatalogItemViewModel Track(CatalogItemViewModel item)
    {
        item.PropertyChanged += OnItemChanged;
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
        var count = AllItems().Count(item => item.IsSelected && !item.IsInstalled);
        SelectedSummary = count > 0 ? string.Format(CultureInfo.CurrentCulture, Texts.Catalog_Selected, count) : null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<CatalogItemViewModel> AllItems() => Groups.SelectMany(group => group.Items);
}
