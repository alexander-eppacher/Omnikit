namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Ein Eintrag in der Navigationsleiste: Titel und die Seite, die dann angezeigt wird.
/// </summary>
public sealed record NavigationItem(string Title, ViewModelBase Page);
