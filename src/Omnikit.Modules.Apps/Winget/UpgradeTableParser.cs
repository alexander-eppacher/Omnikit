namespace Omnikit.Modules.Apps.Winget;

/// <summary>
/// Liest die Tabelle von „winget upgrade“:
/// <code>
/// Name               Id            Version  Available Source
/// -----------------------------------------------------------
/// 7-Zip 26.03 (x64)  7zip.7zip     26.03    26.05     winget
/// </code>
/// Die Überschriften können übersetzt sein („Verfügbar“, „Quelle“). Deshalb zählt nur die Trennlinie aus Bindestrichen,
/// die Anzahl der Spalten und ihre feste Reihenfolge: Name, Id, Version, Verfügbar, Quelle.
/// Gelesen wird jede Zeile von rechts, weil nur der Name Leerzeichen enthalten darf.
/// </summary>
public static class UpgradeTableParser
{
    private const int MinimumSeparatorLength = 10;

    public static IReadOnlyList<AvailableUpgrade> Parse(string output)
    {
        var lines = output.Split('\n').Select(CleanLine).ToList();
        var separator = lines.FindIndex(IsSeparator);
        if (separator < 1)
        {
            return [];
        }

        // Name + Id + Version + Verfügbar (+ Quelle, falls vorhanden)
        var header = lines[separator - 1];
        var headerTokens = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var columnCount = headerTokens.Length;
        if (columnCount is < 4 or > 5)
        {
            return [];
        }

        var idColumnStart = header.IndexOf(" " + headerTokens[1], StringComparison.Ordinal) + 1;

        var result = new List<AvailableUpgrade>();
        foreach (var line in lines.Skip(separator + 1))
        {
            // Die Tabelle endet bei der ersten Leerzeile oder einer zweiten Tabelle.
            if (line.Length == 0 || IsSeparator(line))
            {
                break;
            }

            if (TryParseRow(line, columnCount, idColumnStart, out var upgrade))
            {
                result.Add(upgrade);
            }
        }

        return result;
    }

    private static bool TryParseRow(string line, int columnCount, int idColumnStart, out AvailableUpgrade upgrade)
    {
        upgrade = null!;
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var fields = new List<string>();

        // Von rechts: Quelle, Verfügbar, Version, Id. Versionen wie "< 2.0" bestehen aus zwei Teilen.
        while (fields.Count < columnCount - 1 && tokens.Count > 0)
        {
            var token = tokens[^1];
            tokens.RemoveAt(tokens.Count - 1);
            if (tokens.Count > 0 && tokens[^1] is "<" or ">")
            {
                token = tokens[^1] + " " + token;
                tokens.RemoveAt(tokens.Count - 1);
            }

            fields.Insert(0, token);
        }

        // Am Ende muss noch ein Name übrig sein, und die Id muss gültig aussehen (sonst ist es z. B. ein Hinweissatz).
        // Eine gekürzte Id endet mit "…" und wird später über die Liste der installierten Pakete aufgelöst.
        if (fields.Count != columnCount - 1 || tokens.Count == 0 || !LooksLikeId(fields[0]) || !fields[2].Any(char.IsDigit))
        {
            return false;
        }

        // Die Id steht in ihrer Spalte. Breite Schriftzeichen (z. B. Chinesisch) im Namen verschieben sie höchstens nach links.
        if (line.LastIndexOf(fields[0] + " ", StringComparison.Ordinal) > idColumnStart + 1)
        {
            return false;
        }

        var source = columnCount == 5 ? fields[3] : string.Empty;
        upgrade = new AvailableUpgrade(string.Join(' ', tokens), fields[0], fields[1], fields[2], source);
        return true;
    }

    private static bool LooksLikeId(string id) => PackageId.IsValid(id.TrimEnd('…'));

    private static bool IsSeparator(string line) =>
        line.Length >= MinimumSeparatorLength && line.All(c => c == '-');

    /// <summary>winget zeichnet Fortschrittsanzeigen mit "\r" über dieselbe Zeile; zählt nur der letzte Stand.</summary>
    private static string CleanLine(string line)
    {
        var lastReturn = line.TrimEnd('\r').LastIndexOf('\r');
        var text = lastReturn >= 0 ? line[(lastReturn + 1)..] : line;
        return text.Trim();
    }
}
