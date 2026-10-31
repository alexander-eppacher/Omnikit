using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PcTool.Core.Settings;

namespace PcTool.Desktop.ViewModels;

/// <summary>
/// Der Schalter "Einfach / Experte". Gilt für die ganze App: Alle Seiten hören auf <see cref="IsExpert"/>.
/// Jede Änderung wird sofort in den Einstellungen gespeichert.
/// </summary>
public partial class ViewModeState : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly ILogger<ViewModeState> _logger;
    private AppSettings _settings;

    public ViewModeState(ISettingsStore store, ILogger<ViewModeState> logger)
    {
        _store = store;
        _logger = logger;
        _settings = store.Load();
        IsExpert = _settings.ViewMode == ViewMode.Expert;
    }

    [ObservableProperty]
    public partial bool IsExpert { get; set; }

    public ViewMode Mode => IsExpert ? ViewMode.Expert : ViewMode.Simple;

    partial void OnIsExpertChanged(bool value)
    {
        // Beim Start wird nur der gespeicherte Wert übernommen, dann gibt es nichts zu speichern.
        if (_settings.ViewMode == Mode)
        {
            return;
        }

        _settings = _settings with { ViewMode = Mode };
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Der Schalter wirkt trotzdem, nur beim nächsten Start ist wieder der alte Wert da.
            _logger.LogWarning(ex, "Ansicht konnte nicht gespeichert werden");
        }
    }
}
