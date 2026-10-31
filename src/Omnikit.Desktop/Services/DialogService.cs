using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Omnikit.Desktop.Views;

namespace Omnikit.Desktop.Services;

/// <summary>
/// Zeigt Dialoge über dem Hauptfenster an.
/// </summary>
public sealed class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return false;
        }

        var dialog = new ConfirmationDialog(title, message);
        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<Stream?> PickSaveFileAsync(string suggestedFileName)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });

        return file is null ? null : await file.OpenWriteAsync();
    }

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
