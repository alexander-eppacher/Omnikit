using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PcTool.Core.Modules;
using PcTool.Core.SystemInfo;
using PcTool.Desktop.Resources;
using PcTool.Desktop.Services;

namespace PcTool.Desktop.ViewModels;

/// <summary>
/// Hauptfenster: ermittelt beim Start die Systeminfos und baut die Navigationsleiste auf.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly SystemContextBuilder _contextBuilder;
    private readonly ModuleRegistry _moduleRegistry;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(
        SystemContextBuilder contextBuilder,
        ModuleRegistry moduleRegistry,
        IDialogService dialogs,
        TimeProvider time,
        ILoggerFactory loggerFactory)
    {
        _contextBuilder = contextBuilder;
        _moduleRegistry = moduleRegistry;
        _dialogs = dialogs;
        _time = time;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainViewModel>();
    }

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
                var page = new ModulePageViewModel(module, context, _dialogs, _time, _loggerFactory.CreateLogger<ModulePageViewModel>());
                NavigationItems.Add(new NavigationItem(module.DisplayName, page));
            }

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

    /// <summary>Schreibt die Systeminfos ins Log. Die Seriennummer bewusst nicht (Datenschutz).</summary>
    private void LogContext(SystemContext context)
    {
        _logger.LogInformation(
            "System: {OsName} {OsVersion} (Build {OsBuild}), Modell: {Manufacturer} {Model}, VM: {VmType}, Admin: {IsAdmin}, Laptop: {IsLaptop}",
            context.OsName, context.OsDisplayVersion, context.OsBuild,
            context.Manufacturer, context.Model, context.VmType, context.IsAdmin, context.IsLaptop);
    }
}
