using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Desktop.Resources;
using Omnikit.Desktop.Services;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Hauptfenster: ermittelt beim Start die Systeminfos und baut die Navigationsleiste auf.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly SystemContextBuilder _contextBuilder;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly Func<SystemContext, PerformancePageViewModel> _createPerformancePage;
    private readonly Func<SystemContext, AppsPageViewModel> _createAppsPage;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(
        SystemContextBuilder contextBuilder,
        ModuleRegistry moduleRegistry,
        IDialogService dialogs,
        TimeProvider time,
        ViewModeState viewMode,
        Func<SystemContext, PerformancePageViewModel> createPerformancePage,
        Func<SystemContext, AppsPageViewModel> createAppsPage,
        ILoggerFactory loggerFactory)
    {
        _createPerformancePage = createPerformancePage;
        _createAppsPage = createAppsPage;
        ViewMode = viewMode;
        _contextBuilder = contextBuilder;
        _moduleRegistry = moduleRegistry;
        _dialogs = dialogs;
        _time = time;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainViewModel>();
    }

    /// <summary>Schalter "Einfach / Experte" unten in der Navigationsleiste.</summary>
    public ViewModeState ViewMode { get; }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public async Task InitializeAsync()
    {
        try
        {
            // WMI-Abfragen dauern etwas: im Hintergrund ausführen, damit das Fenster nicht einfriert.
            var context = await Task.Run(_contextBuilder.Build);
            LogContext(context);

            NavigationItems.Add(new NavigationItem(Strings.Nav_Overview, new OverviewViewModel(context)));
            foreach (var module in _moduleRegistry.GetSupported(context))
            {
                var page = new ModulePageViewModel(module, context, _dialogs, _time, ViewMode, _loggerFactory.CreateLogger<ModulePageViewModel>());
                NavigationItems.Add(new NavigationItem(module.DisplayName, page));
            }

            NavigationItems.Add(new NavigationItem(Strings.Nav_Performance, _createPerformancePage(context)));
            NavigationItems.Add(new NavigationItem(Omnikit.Modules.Apps.Resources.AppsTexts.PageTitle, _createAppsPage(context)));

            SelectedItem = NavigationItems[0];
        }
#pragma warning disable CA1031 // Beim Start soll jeder Fehler verständlich angezeigt werden, statt die App abstürzen zu lassen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Systeminformationen konnten nicht ermittelt werden");
            ErrorMessage = Strings.Main_LoadError;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Beim Beenden: laufende Messungen stoppen.</summary>
    public void Shutdown() => (SelectedItem?.Page as IActivatablePage)?.Deactivate();

    // Seitenwechsel: Die alte Seite hört auf zu messen, die neue fängt an (nur Seiten mit IActivatablePage).
    partial void OnSelectedItemChanged(NavigationItem? oldValue, NavigationItem? newValue)
    {
        (oldValue?.Page as IActivatablePage)?.Deactivate();
        (newValue?.Page as IActivatablePage)?.Activate();
    }

    /// <summary>Schreibt die Systeminfos ins Log. Die Seriennummer bewusst nicht (Datenschutz).</summary>
    private void LogContext(SystemContext context)
    {
        _logger.LogInformation(
            "System: {OsName} {OsVersion} (Build {OsBuild}), Modell: {Manufacturer} {Model}, VM: {VmType}, Admin: {IsAdmin}, Laptop: {IsLaptop}",
            context.OsName, context.OsDisplayVersion, context.OsBuild,
            context.Manufacturer, context.Model, context.VmType, context.IsAdmin, context.IsLaptop);
    }
}
