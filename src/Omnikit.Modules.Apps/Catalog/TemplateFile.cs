using System.Text.Json;
using Omnikit.Modules.Apps.Winget;

namespace Omnikit.Modules.Apps.Catalog;

/// <summary>
/// Speichert eine eigene Auswahl als JSON-Datei und liest sie wieder ein, z. B. um dieselben Programme auf mehreren PCs zu installieren.
/// <code>{ "SchemaVersion": 1, "Name": "Computerraum", "Packages": [ "Mozilla.Firefox", "7zip.7zip" ] }</code>
/// Beim Einlesen werden ungültige Einträge verworfen: Die Datei kann von irgendwoher stammen.
/// </summary>
public static class TemplateFile
{
    public const int SchemaVersion = 1;
    private const int MaxPackages = 200;
    private const int MaxNameLength = 80;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Task SaveAsync(Stream stream, AppTemplate template, CancellationToken cancellationToken) =>
        JsonSerializer.SerializeAsync(
            stream, new FileContent(SchemaVersion, template.Name, [.. template.PackageIds]), JsonOptions, cancellationToken);

    /// <returns>Die Vorlage, oder <c>null</c>, wenn die Datei keine gültige Vorlage ist.</returns>
    public static async Task<AppTemplate?> LoadAsync(Stream stream, string fallbackName, CancellationToken cancellationToken)
    {
        FileContent? content;
        try
        {
            content = await JsonSerializer.DeserializeAsync<FileContent>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }

        if (content?.Packages is null || content.SchemaVersion != SchemaVersion)
        {
            return null;
        }

        var packages = content.Packages
            .Where(PackageId.IsValid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxPackages)
            .ToList();

        var name = string.IsNullOrWhiteSpace(content.Name) ? fallbackName : content.Name.Trim();
        return packages.Count == 0 ? null : new AppTemplate(name.Length > MaxNameLength ? name[..MaxNameLength] : name, packages);
    }

    private sealed record FileContent(int SchemaVersion, string? Name, List<string>? Packages);
}
