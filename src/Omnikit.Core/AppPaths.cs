namespace Omnikit.Core;

/// <summary>
/// Ordner, in denen die App ihre eigenen Dateien ablegt.
/// Windows: %APPDATA%\Omnikit und %LOCALAPPDATA%\Omnikit. Linux: ~/.config/Omnikit und ~/.local/share/Omnikit.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "Omnikit";

    public static string SettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, "settings.json");

    /// <summary>Einstellungen aus der Zeit, als die App noch „PcTool“ hieß (bis 31.10.2026). Werden einmalig übernommen.</summary>
    public static string LegacySettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcTool", "settings.json");

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "logs");

    /// <summary>Für kurzlebige Dateien, z. B. den Akku-Bericht von powercfg. Dateien hier werden nach Gebrauch gelöscht.</summary>
    public static string TempDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "temp");
}
