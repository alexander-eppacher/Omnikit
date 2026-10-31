namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Eine Zeile in der Übersicht, z. B. "Modell: OptiPlex 7090".
/// </summary>
/// <param name="Note">Optionaler Hinweis unter dem Wert, z. B. zum Datenschutz.</param>
public sealed record InfoRow(string Label, string Value, string? Note = null);
