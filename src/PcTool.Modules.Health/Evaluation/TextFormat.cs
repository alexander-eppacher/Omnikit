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

    /// <summary>Anteil in ganzen Prozent, z. B. (42090, 45000) → 94.</summary>
    public static int Percent(long part, long total) =>
        total <= 0 ? 0 : (int)Math.Round(100.0 * part / total, MidpointRounding.AwayFromZero);
}
