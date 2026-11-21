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
    /// <param name="fileTypeName">Anzeigename des Dateityps, z. B. "JSON" oder "ZIP".</param>
    /// <param name="extension">Dateiendung ohne Punkt, z. B. "json".</param>
    /// <returns>Stream zum Schreiben, oder <c>null</c>, wenn abgebrochen wurde.</returns>
    Task<Stream?> PickSaveFileAsync(string suggestedFileName, string fileTypeName = "JSON", string extension = "json");

    /// <summary>Lässt den Nutzer eine JSON-Datei zum Öffnen wählen.</summary>
    /// <returns>Stream zum Lesen, oder <c>null</c>, wenn abgebrochen wurde.</returns>
    Task<Stream?> PickOpenFileAsync();

    /// <summary>Lässt den Nutzer eine Datei wählen und gibt ihren Pfad zurück (z. B. eine ISO für den Hilfsprozess).</summary>
    /// <returns>Vollständiger Pfad, oder <c>null</c>, wenn abgebrochen wurde oder die Datei keinen lokalen Pfad hat.</returns>
    Task<string?> PickOpenFilePathAsync(string fileTypeName, string extension);
}
