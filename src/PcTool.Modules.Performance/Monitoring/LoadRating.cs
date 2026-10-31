using PcTool.Core.Modules;
using PcTool.Modules.Performance.Resources;

namespace PcTool.Modules.Performance.Monitoring;

/// <summary>
/// Bewertet die Auslastung. Bewertet wird der Durchschnitt über 30 Sekunden, damit kurze Spitzen
/// (z. B. beim Öffnen eines Programms) keinen Hinweis auslösen. Höchstens „Hinweis“, nie Warnung:
/// Hohe Last ist kein Defekt, sondern sagt nur, dass der PC gerade viel zu tun hat.
/// </summary>
public static class LoadRating
{
    public const int HighFromPercent = 90;
    public const int WindowSeconds = 30;

    public static Severity Rate(LoadHistory history) =>
        history.AverageOfLast(WindowSeconds) >= HighFromPercent ? Severity.Info : Severity.Ok;

    /// <summary>Hat die Bewertung schon genug Werte (30 Sekunden)?</summary>
    public static bool HasEnoughData(LoadHistory history) => history.Count >= WindowSeconds;

    /// <summary>Wort für Einsteiger zur momentanen Auslastung von Prozessor, Laufwerk oder Grafik.</summary>
    public static string Describe(double percent) => percent switch
    {
        < 30 => PerformanceTexts.Word_Low,
        < 70 => PerformanceTexts.Word_Medium,
        < HighFromPercent => PerformanceTexts.Word_High,
        _ => PerformanceTexts.Word_Full,
    };

    /// <summary>Beim Arbeitsspeicher ist „gut gefüllt“ normal: Windows nutzt freien Speicher als Zwischenspeicher.</summary>
    public static string DescribeMemory(double usedPercent) => usedPercent switch
    {
        < 60 => PerformanceTexts.Word_MemoryLow,
        < HighFromPercent => PerformanceTexts.Word_MemoryMedium,
        _ => PerformanceTexts.Word_MemoryFull,
    };
}
