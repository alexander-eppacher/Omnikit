namespace Omnikit.Modules.Apps.Catalog;

/// <summary>Bereiche im Katalog, in dieser Reihenfolge angezeigt.</summary>
public enum AppCategory
{
    Browser,
    Communication,
    Office,
    Media,
    Tools,
    Learning,
    Games,
    Development,
}

/// <summary>Ein Programm im Katalog.</summary>
/// <param name="PackageId">winget-Paket-ID, z. B. "Mozilla.Firefox".</param>
/// <param name="Name">Produktname (wird nicht übersetzt).</param>
/// <param name="Description">Kurze Erklärung für Einsteiger.</param>
public sealed record CatalogApp(string PackageId, string Name, AppCategory Category, string Description);

/// <summary>Eine Vorlage: eine fertige Auswahl von Programmen, z. B. „Schul-PC“.</summary>
public sealed record AppTemplate(string Name, IReadOnlyList<string> PackageIds);
