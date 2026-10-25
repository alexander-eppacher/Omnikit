namespace PcTool.Core.Modules;

/// <summary>
/// Eine Aktion, die ein Modul anbietet (z. B. "Programm aktualisieren").
/// Vor dem Ausführen zeigt die UI immer einen Bestätigungsdialog.
/// </summary>
/// <param name="Id">Technische Kennung, eindeutig innerhalb des Moduls.</param>
/// <param name="DisplayName">Name für die Schaltfläche.</param>
/// <param name="ConfirmationText">Text für den Bestätigungsdialog: sagt klar, was passiert.</param>
/// <param name="ConfirmationTitle">Optionale Überschrift des Dialogs; sonst wird <paramref name="DisplayName"/> verwendet.</param>
public sealed record ModuleAction(string Id, string DisplayName, string ConfirmationText, string? ConfirmationTitle = null);
