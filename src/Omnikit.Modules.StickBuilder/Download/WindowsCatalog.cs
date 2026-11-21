using System.Globalization;
using System.Xml.Linq;

namespace Omnikit.Modules.StickBuilder.Download;

/// <summary>Eine offizielle Windows-11-Datei (ESD) aus dem Katalog des Media Creation Tool.</summary>
/// <param name="LanguageCode">z. B. "de-de".</param>
/// <param name="Language">Name aus dem Katalog, z. B. "German (Germany)".</param>
/// <param name="Sha1">Prüfsumme aus dem Katalog (hexadezimal, klein geschrieben).</param>
public sealed record WindowsDownload(string LanguageCode, string Language, string FileName, long SizeBytes, string Sha1, Uri Uri)
{
    public string DisplayName => string.Format(CultureInfo.CurrentCulture, "{0} ({1:0.0} GB)", Language, SizeBytes / 1e9);

    public override string ToString() => DisplayName;
}

/// <summary>
/// Liest den Katalog, den auch das Media Creation Tool von Microsoft verwendet (products.xml aus products.cab).
/// Er nennt für jede Sprache die offizielle Windows-Datei mit Größe und Prüfsumme.
/// </summary>
public static class WindowsCatalog
{
    /// <summary>Offizieller Link von Microsoft auf den Katalog für Windows 11 (führt per HTTPS zu download.microsoft.com).</summary>
    public static readonly Uri CatalogUri = new("https://go.microsoft.com/fwlink/?LinkId=2156292");

    /// <summary>
    /// Windows-11-Dateien für PCs mit Intel/AMD-Prozessor, je Sprache eine. „CLIENTCONSUMER“ enthält Home, Pro und Education.
    /// Nur Adressen bei Microsoft werden übernommen.
    /// </summary>
    public static IReadOnlyList<WindowsDownload> Parse(string productsXml)
    {
        var result = new Dictionary<string, WindowsDownload>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in XDocument.Parse(productsXml).Descendants("File"))
        {
            var name = (string?)file.Element("FileName") ?? string.Empty;
            var language = ((string?)file.Element("LanguageCode") ?? string.Empty).ToLowerInvariant();
            if ((string?)file.Element("Architecture") != "x64"
                || !name.Contains("CLIENTCONSUMER", StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".esd", StringComparison.OrdinalIgnoreCase)
                || language.Length == 0
                || result.ContainsKey(language)
                || !long.TryParse((string?)file.Element("Size"), NumberStyles.None, CultureInfo.InvariantCulture, out var size)
                || ((string?)file.Element("Sha1"))?.ToLowerInvariant() is not { Length: 40 } sha1
                || !Uri.TryCreate((string?)file.Element("FilePath"), UriKind.Absolute, out var uri)
                || !IsMicrosoftHost(uri))
            {
                continue;
            }

            result[language] = new WindowsDownload(language, (string?)file.Element("Language") ?? language, name, size, sha1, uri);
        }

        return result.Values.OrderBy(download => download.Language, StringComparer.CurrentCulture).ToList();
    }

    internal static bool IsMicrosoftHost(Uri uri) =>
        uri.Scheme is "http" or "https"
        && (uri.Host.Equals("microsoft.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase));
}
