using Microsoft.Extensions.Logging;

namespace Omnikit.Core.Settings;

/// <summary>
/// Übernimmt die Einstellungen aus dem alten Ordner „PcTool“ (früherer Name der App), damit nach dem
/// Umbenennen nichts verloren geht. Es wird nur kopiert, nie gelöscht oder überschrieben.
/// </summary>
public static class LegacySettings
{
    public static void CopyIfMissing(string legacyPath, string path, ILogger logger)
    {
        if (File.Exists(path) || !File.Exists(legacyPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(legacyPath, path);
            logger.LogInformation("Einstellungen aus dem alten PcTool-Ordner übernommen");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nicht schlimm: Die App startet dann mit Standardwerten.
            logger.LogWarning(ex, "Alte Einstellungen konnten nicht übernommen werden");
        }
    }
}
