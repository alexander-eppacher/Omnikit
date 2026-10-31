namespace Omnikit.Core.Settings;

/// <summary>
/// Lädt und speichert die <see cref="AppSettings"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Lädt die Einstellungen. Fehlt die Datei oder ist sie kaputt, gibt es die Standardwerte.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
