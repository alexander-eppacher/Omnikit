using System.Globalization;

namespace Omnikit.Modules.Performance.Monitoring;

/// <summary>
/// Formatiert Messwerte lesbar, im Zahlenformat von Windows (z. B. "12,3 MB/s" auf deutschem Windows).
/// </summary>
public static class Units
{
    public static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, template, values);

    /// <summary>z. B. 29313187840 → "27,3 GB" (1 GB = 1024³ Bytes, wie im Task-Manager).</summary>
    public static string Bytes(double bytes) => Scale(bytes, 1024, ["B", "KB", "MB", "GB", "TB"]);

    /// <summary>Laufwerke: z. B. "12,3 MB/s".</summary>
    public static string BytesPerSecond(double bytes) => Scale(bytes, 1024, ["B/s", "KB/s", "MB/s", "GB/s"]);

    /// <summary>Netzwerk wird in Bit gemessen, mit 1000er-Schritten: z. B. "12,3 Mbit/s".</summary>
    public static string BitsPerSecond(double bits) => Scale(bits, 1000, ["bit/s", "kbit/s", "Mbit/s", "Gbit/s"]);

    /// <summary>z. B. 3210 → "3,21 GHz".</summary>
    public static string Megahertz(double mhz) => (mhz / 1000).ToString("0.00", CultureInfo.CurrentCulture) + " GHz";

    /// <summary>Prozent ohne Nachkommastellen, z. B. "23 %".</summary>
    public static string Percent(double percent) =>
        Math.Round(Math.Clamp(percent, 0, 100), MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture) + " %";

    /// <summary>Betriebszeit wie im Task-Manager: Tage:Stunden:Minuten:Sekunden, z. B. "2:05:31:10".</summary>
    public static string Uptime(TimeSpan uptime) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)uptime.TotalDays}:{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}");

    private static string Scale(double value, double step, string[] units)
    {
        var unit = 0;
        while (Math.Abs(value) >= step && unit < units.Length - 1)
        {
            value /= step;
            unit++;
        }

        // Kleine Einheit ohne Nachkommastelle ("512 KB/s"), große mit einer ("12,3 MB/s").
        var format = unit == 0 || value >= 100 ? "0" : "0.0";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
