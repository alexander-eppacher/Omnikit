namespace Omnikit.Modules.StickBuilder;

/// <summary>Ein eingesteckter USB-Stick, auf dem ein Windows-Installationsmedium liegt.</summary>
/// <param name="Root">Stammverzeichnis, z. B. "E:\".</param>
/// <param name="Label">Name des Laufwerks, z. B. "ESD-USB".</param>
/// <param name="Languages">Sprachen des Windows-Images laut <c>sources\lang.ini</c>, z. B. ["de-DE"]; leer = unbekannt.</param>
/// <param name="HasAnswerFile">Es liegt schon eine <c>autounattend.xml</c> darauf (wird beim Schreiben ersetzt).</param>
public sealed record WindowsStick(string Root, string Label, IReadOnlyList<string> Languages, bool HasAnswerFile)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Root : $"{Label} ({Root.TrimEnd('\\')})";

    public override string ToString() => DisplayName;
}

/// <summary>
/// Findet Windows-Installationssticks und schreibt die Antwortdatei darauf. Geschrieben wird ausschließlich
/// <c>autounattend.xml</c> ins Stammverzeichnis – sonst nichts am Stick.
/// </summary>
public static class WindowsStickFinder
{
    // Windows-Abbild auf Installationsmedien: einzeln (.wim/.esd) oder aufgeteilt (.swm, für FAT32-Sticks)
    private static readonly string[] ImageNames = ["install.wim", "install.esd", "install.swm"];

    /// <summary>Alle Wechseldatenträger mit Windows-Setup (Ordner „sources“ mit install.wim/.esd/.swm).</summary>
    public static IReadOnlyList<WindowsStick> Find() =>
        DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Removable && drive.IsReady)
            .Select(drive => Inspect(drive.RootDirectory.FullName, drive.VolumeLabel))
            .OfType<WindowsStick>()
            .ToList();

    /// <returns>Den Stick, oder <c>null</c>, wenn dort kein Windows-Setup liegt.</returns>
    public static WindowsStick? Inspect(string root, string label)
    {
        var sources = Path.Combine(root, "sources");
        var hasImage = ImageNames.Any(name => File.Exists(Path.Combine(sources, name)));
        if (!hasImage)
        {
            return null;
        }

        var languages = File.Exists(Path.Combine(sources, "lang.ini"))
            ? ReadLanguages(File.ReadAllLines(Path.Combine(sources, "lang.ini")))
            : [];
        return new WindowsStick(root, label, languages, File.Exists(Path.Combine(root, StickPackage.AnswerFileName)));
    }

    /// <summary>
    /// Liest die Sprachen aus <c>lang.ini</c>:
    /// <code>[Available UI Languages]
    /// de-DE = 3</code>
    /// </summary>
    internal static IReadOnlyList<string> ReadLanguages(IEnumerable<string> lines)
    {
        var result = new List<string>();
        var inSection = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inSection = line.Equals("[Available UI Languages]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (inSection && separator > 0)
            {
                result.Add(line[..separator].Trim());
            }
        }

        return result;
    }

    /// <summary>
    /// Prüft eine vorhandene Antwortdatei (z. B. vom Schneegans-Generator), bevor sie importiert wird:
    /// gültiges XML mit dem Wurzelelement „unattend“ von Microsoft und höchstens 1 MB.
    /// </summary>
    public static bool IsAnswerFile(string content)
    {
        if (content.Length > 1_000_000)
        {
            return false;
        }

        try
        {
            var root = System.Xml.Linq.XDocument.Parse(content).Root;
            return root is not null && root.Name == System.Xml.Linq.XName.Get("unattend", "urn:schemas-microsoft-com:unattend");
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    /// <summary>Legt eine vorhandene, geprüfte Antwortdatei auf den Stick. Nur nach Bestätigung durch den Nutzer aufrufen.</summary>
    public static Task ImportAnswerFileAsync(WindowsStick stick, string content, CancellationToken cancellationToken) =>
        IsAnswerFile(content)
            ? File.WriteAllTextAsync(Path.Combine(stick.Root, StickPackage.AnswerFileName), content, new System.Text.UTF8Encoding(false), cancellationToken)
            : throw new ArgumentException("Keine gültige Antwortdatei.", nameof(content));

    /// <summary>Schreibt die Antwortdatei ins Stammverzeichnis. Nur nach Bestätigung durch den Nutzer aufrufen.</summary>
    public static async Task WriteAnswerFileAsync(WindowsStick stick, StickOptions options, CancellationToken cancellationToken)
    {
        var content = AnswerFile.Create(options);
        await File.WriteAllTextAsync(Path.Combine(stick.Root, StickPackage.AnswerFileName), content, new System.Text.UTF8Encoding(false), cancellationToken);
    }
}
