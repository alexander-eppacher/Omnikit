namespace PcTool.Core;

/// <summary>
/// Ordner, in denen die App ihre eigenen Dateien ablegt.
/// Windows: %APPDATA%\PcTool und %LOCALAPPDATA%\PcTool. Linux: ~/.config/PcTool und ~/.local/share/PcTool.
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "PcTool";

    public static string SettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, "settings.json");

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "logs");

    /// <summary>Für kurzlebige Dateien, z. B. den Akku-Bericht von powercfg. Dateien hier werden nach Gebrauch gelöscht.</summary>
    public static string TempDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "temp");
}
