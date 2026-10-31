using System.Text.Json;

namespace Omnikit.Modules.Apps.Winget;

/// <summary>
/// Liest die Datei von „winget export“. Sie ist JSON und enthält die vollständigen Paket-IDs –
/// zuverlässiger als die Tabelle von „winget list“, deren Spalten gekürzt sein können.
/// </summary>
public static class ExportParser
{
    /// <returns>Installierte Pakete; leer, wenn die Datei kein gültiges Exportformat hat.</returns>
    public static IReadOnlyList<InstalledPackage> Parse(string json)
    {
        var result = new List<InstalledPackage>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("Sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var source in sources.EnumerateArray())
            {
                var sourceName = source.TryGetProperty("SourceDetails", out var details) && details.TryGetProperty("Name", out var name)
                    ? name.GetString() ?? string.Empty
                    : string.Empty;

                if (!source.TryGetProperty("Packages", out var packages) || packages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var package in packages.EnumerateArray())
                {
                    var id = package.TryGetProperty("PackageIdentifier", out var idValue) ? idValue.GetString() : null;
                    var version = package.TryGetProperty("Version", out var versionValue) ? versionValue.GetString() : null;
                    if (PackageId.IsValid(id))
                    {
                        result.Add(new InstalledPackage(id!, version ?? string.Empty, sourceName));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Unbekanntes Format: lieber nichts melden als falsche Werte
        }

        return result;
    }
}
