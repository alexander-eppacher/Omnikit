using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PcTool.Core.Settings;

/// <summary>
/// Speichert die Einstellungen als lesbare JSON-Datei.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Auswahlwerte als Text ("Expert") statt als Zahl, damit man die Datei auch von Hand lesen kann.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger<JsonSettingsStore> _logger;

    public JsonSettingsStore(string filePath, ILogger<JsonSettingsStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("Keine Einstellungsdatei gefunden, verwende Standardwerte: {Path}", _filePath);
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Einstellungsdatei konnte nicht gelesen werden, verwende Standardwerte: {Path}", _filePath);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Erst in eine Hilfsdatei schreiben und dann umbenennen:
        // So bleibt bei einem Absturz mitten im Schreiben die alte Datei heil.
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
