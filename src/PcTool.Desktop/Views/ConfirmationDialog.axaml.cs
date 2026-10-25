using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PcTool.Desktop.Views;

/// <summary>
/// Einfacher Bestätigungsdialog: Text plus "Abbrechen" und "Weiter".
/// Gibt beim Schließen <c>true</c> (Weiter) oder <c>false</c> (Abbrechen/Fenster geschlossen) zurück.
/// </summary>
public partial class ConfirmationDialog : Window
{
    // Für den Avalonia-Designer nötig.
    public ConfirmationDialog() : this(string.Empty, string.Empty)
    {
    }

    public ConfirmationDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnContinue(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
