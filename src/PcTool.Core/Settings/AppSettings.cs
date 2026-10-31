namespace PcTool.Core.Settings;

/// <summary>
/// Einstellungen der App. Werden als JSON im Benutzerprofil gespeichert.
/// Neue Einstellungen kommen mit den Modulen dazu; fehlen sie in einer alten Datei, gilt der Standardwert.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Version des Dateiformats, damit spätere Versionen alte Dateien erkennen können.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Einfache oder Expertenansicht (Schalter unten in der Navigationsleiste).</summary>
    public ViewMode ViewMode { get; init; } = ViewMode.Simple;
}
