namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Seite „Einstellungen“: Ansicht (Einfach/Experte), Update-Suche und installierte Version an einem Ort.
/// Hält selbst nichts; die Werte gehören den gemeinsamen Objekten, die auch der Rest der App benutzt.
/// </summary>
public sealed class SettingsViewModel(ViewModeState viewMode, UpdateViewModel update) : ViewModelBase
{
    public ViewModeState ViewMode { get; } = viewMode;

    public UpdateViewModel Update { get; } = update;
}
