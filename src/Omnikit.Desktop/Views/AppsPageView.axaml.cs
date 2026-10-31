using Avalonia.Controls;
using Avalonia.Input;
using Omnikit.Desktop.ViewModels;

namespace Omnikit.Desktop.Views;

public partial class AppsPageView : UserControl
{
    public AppsPageView()
    {
        InitializeComponent();
    }

    /// <summary>Ein Klick auf Name oder Beschreibung setzt das Häkchen, wie bei einer normalen Checkbox.</summary>
    private void OnCatalogTextTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: CatalogItemViewModel item } && item.CanChange)
        {
            item.IsSelected = !item.IsSelected;
        }
    }
}
