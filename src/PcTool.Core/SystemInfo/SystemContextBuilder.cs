namespace PcTool.Core.SystemInfo;

/// <summary>
/// Macht aus den Rohdaten einen bereinigten <see cref="SystemContext"/>:
/// leere Werte und Platzhalter der Hersteller werden zu "nicht verfügbar", die VM-Erkennung läuft.
/// </summary>
public sealed class SystemContextBuilder
{
    // Platzhalter, die Hersteller statt echter Werte ins BIOS schreiben.
    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "To be filled by O.E.M.",
        "Default string",
        "System Serial Number",
        "System manufacturer",
        "System Product Name",
        "Not Applicable",
        "None",
        "0",
    };

    private readonly ISystemInfoProvider _provider;

    public SystemContextBuilder(ISystemInfoProvider provider)
    {
        _provider = provider;
    }

    public SystemContext Build()
    {
        var raw = _provider.Read();
        var manufacturer = Clean(raw.Manufacturer);
        var model = Clean(raw.Model);

        return new SystemContext
        {
            OsName = Clean(raw.OsName),
            OsDisplayVersion = Clean(raw.OsDisplayVersion),
            OsBuild = Clean(raw.OsBuild),
            Manufacturer = manufacturer,
            Model = BuildModelName(manufacturer, model, Clean(raw.ProductVersion)),
            SerialNumber = Clean(raw.SerialNumber),
            // Für die VM-Erkennung die Originalwerte nehmen, nicht den zusammengesetzten Anzeigenamen.
            VmType = VmDetector.Detect(manufacturer, model),
            IsLaptop = raw.HasBattery,
            IsAdmin = raw.IsAdmin,
            IsAdminAccount = raw.IsAdminAccount,
            IsWindows = raw.IsWindows,
        };
    }

    /// <summary>
    /// Lenovo schreibt nur die Typnummer ins Modell-Feld ("21MV"); der lesbare Name steht in der Produktversion.
    /// Dann wird daraus "ThinkBook 14 G7 ARP (21MV)". Andere Hersteller nutzen die Produktversion
    /// uneinheitlich (oft "1.0" oder leer), deshalb gilt das nur für Lenovo.
    /// </summary>
    internal static string? BuildModelName(string? manufacturer, string? model, string? productVersion)
    {
        var isLenovo = manufacturer?.Contains("Lenovo", StringComparison.OrdinalIgnoreCase) == true;

        if (!isLenovo || productVersion is null || model is null)
        {
            return model;
        }

        return productVersion.Contains(model, StringComparison.OrdinalIgnoreCase)
            ? productVersion
            : $"{productVersion} ({model})";
    }

    /// <summary>Entfernt Leerzeichen am Rand; leere Werte und bekannte Platzhalter werden zu <c>null</c>.</summary>
    internal static string? Clean(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || PlaceholderValues.Contains(trimmed))
        {
            return null;
        }

        return trimmed;
    }
}
