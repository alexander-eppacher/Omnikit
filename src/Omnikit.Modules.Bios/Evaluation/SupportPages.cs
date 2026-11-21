namespace Omnikit.Modules.Bios.Evaluation;

/// <summary>Offizielle Support-Seite eines Herstellers (dort gibt es BIOS-Updates und Anleitungen).</summary>
public sealed record SupportPage(string Name, Uri Url);

/// <summary>
/// Ordnet den Hersteller der Support-Seite zu. Nur große Hersteller mit fester, offizieller Adresse;
/// für alle anderen gibt es keinen Link statt eines geratenen.
/// </summary>
public static class SupportPages
{
    // Platzhalter, die Mainboard-Hersteller bei selbst gebauten PCs als „Systemhersteller“ eintragen
    private static readonly string[] Placeholders = ["System manufacturer", "To Be Filled By O.E.M.", "Default string", "OEM"];

    private static readonly (string Match, SupportPage Page)[] Known =
    [
        ("lenovo", new("Lenovo", new Uri("https://pcsupport.lenovo.com/"))),
        ("hewlett", new("HP", new Uri("https://support.hp.com/drivers"))),
        ("dell", new("Dell", new Uri("https://www.dell.com/support/home"))),
        ("asus", new("ASUS", new Uri("https://www.asus.com/support/download-center/"))),
        ("acer", new("Acer", new Uri("https://www.acer.com/support"))),
        ("micro-star", new("MSI", new Uri("https://www.msi.com/support"))),
        ("gigabyte", new("Gigabyte", new Uri("https://www.gigabyte.com/Support"))),
        ("asrock", new("ASRock", new Uri("https://www.asrock.com/support/"))),
    ];

    /// <param name="systemManufacturer">Hersteller des PCs (WMI Win32_ComputerSystem).</param>
    /// <param name="model">Modell des PCs, z. B. „Surface Laptop 5“.</param>
    /// <param name="boardManufacturer">Hersteller der Hauptplatine; zählt, wenn der PC-Hersteller nur ein Platzhalter ist.</param>
    public static SupportPage? Find(string? systemManufacturer, string? model, string? boardManufacturer)
    {
        var manufacturer = systemManufacturer is null || Placeholders.Contains(systemManufacturer.Trim(), StringComparer.OrdinalIgnoreCase)
            ? boardManufacturer
            : systemManufacturer;

        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return null;
        }

        // „HP“ allein ist zu kurz für eine Teilsuche; Microsoft nur bei Surface (sonst z. B. Hyper-V)
        if (manufacturer.Trim().Equals("HP", StringComparison.OrdinalIgnoreCase))
        {
            return Known.Single(entry => entry.Match == "hewlett").Page;
        }

        if (manufacturer.Contains("microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return model?.Contains("surface", StringComparison.OrdinalIgnoreCase) == true
                ? new SupportPage("Microsoft Surface", new Uri("https://support.microsoft.com/surface"))
                : null;
        }

        return Known.FirstOrDefault(entry => manufacturer.Contains(entry.Match, StringComparison.OrdinalIgnoreCase)).Page;
    }
}
