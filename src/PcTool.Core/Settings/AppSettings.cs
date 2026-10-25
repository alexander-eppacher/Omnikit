namespace PcTool.Core.Settings;

/// <summary>
/// Einstellungen der App. Werden als JSON im Benutzerprofil gespeichert.
/// In Phase 0 noch fast leer; neue Einstellungen kommen mit den Modulen dazu.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Version des Dateiformats, damit spätere Versionen alte Dateien erkennen können.</summary>
    public int SchemaVersion { get; init; } = 1;
}
