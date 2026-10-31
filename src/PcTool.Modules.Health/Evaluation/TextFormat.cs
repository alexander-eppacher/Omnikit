using System.Globalization;

namespace PcTool.Modules.Health.Evaluation;

/// <summary>
/// Kleine Helfer, um Werte für Befundtexte lesbar zu formatieren.
/// </summary>
internal static class TextFormat
{
    public static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, template, values);

    /// <summary>z. B. 512110190592 → "476,9 GB".</summary>
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : "0.0", CultureInfo.CurrentCulture) + " " + units[unit];
    }

    /// <summary>
    /// Reiht Rohwerte für die Expertenansicht aneinander, z. B. "Anschluss: NVMe · Firmware: 1.0".
    /// Fehlende Werte (<c>null</c>) werden übersprungen; gibt es gar keine, ist das Ergebnis <c>null</c>.
    /// </summary>
    public static string? Details(params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrEmpty(part)).ToList();
        return present.Count == 0 ? null : string.Join(" · ", present);
    }

    /// <summary>Wie <see cref="Format"/>, aber <c>null</c>, wenn der Wert fehlt. Für <see cref="Details"/>.</summary>
    public static string? FormatIf(string template, object? value) =>
        value is null ? null : Format(template, value);

    /// <summary>Anteil in ganzen Prozent, z. B. (42090, 45000) → 94.</summary>
    public static int Percent(long part, long total) =>
        total <= 0 ? 0 : (int)Math.Round(100.0 * part / total, MidpointRounding.AwayFromZero);
}
