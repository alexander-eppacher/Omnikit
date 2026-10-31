namespace Omnikit.Desktop.Services;

/// <summary>
/// Dialoge, die ViewModels brauchen, ohne selbst Fenster zu kennen.
/// </summary>
public interface IDialogService
{
    /// <summary>Zeigt einen Bestätigungsdialog mit "Weiter" und "Abbrechen".</summary>
    /// <returns><c>true</c>, wenn der Nutzer "Weiter" gewählt hat.</returns>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Lässt den Nutzer einen Speicherort wählen.</summary>
    /// <returns>Stream zum Schreiben, oder <c>null</c>, wenn abgebrochen wurde.</returns>
    Task<Stream?> PickSaveFileAsync(string suggestedFileName);

    /// <summary>Lässt den Nutzer eine JSON-Datei zum Öffnen wählen.</summary>
    /// <returns>Stream zum Lesen, oder <c>null</c>, wenn abgebrochen wurde.</returns>
    Task<Stream?> PickOpenFileAsync();
}
