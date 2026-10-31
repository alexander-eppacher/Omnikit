using System.Globalization;
using System.Text.RegularExpressions;

namespace Omnikit.Modules.Performance.Monitoring;

/// <summary>
/// Windows liefert Leistungsindikatoren mit „Instanznamen“ (z. B. "0,3" für den vierten Kern oder "0 C:" für Laufwerk 0).
/// Diese Klasse übersetzt die Namen in verständliche Werte. Reine Textverarbeitung, deshalb gut testbar.
/// </summary>
public static partial class CounterInstances
{
    /// <summary>
    /// Werte je logischem Prozessor in der richtigen Reihenfolge. Instanzen heißen "Gruppe,Nummer" (z. B. "0,11");
    /// Summen wie "_Total" oder "0,_Total" werden ausgelassen. Ohne Sortierung käme "0,10" vor "0,2".
    /// </summary>
    public static IReadOnlyList<double> OrderCores(IEnumerable<KeyValuePair<string, double>> instances) =>
        instances
            .Select(instance => (Key: ParseCore(instance.Key), instance.Value))
            .Where(instance => instance.Key is not null)
            .OrderBy(instance => instance.Key!.Value.Group)
            .ThenBy(instance => instance.Key!.Value.Index)
            .Select(instance => instance.Value)
            .ToList();

    /// <summary>
    /// Laufwerk aus dem Instanznamen von "PhysicalDisk", z. B. "0 C:" → (0, ["C:"]), "1 D: E:" → (1, ["D:", "E:"]).
    /// "_Total" oder Unlesbares ergibt <c>null</c>.
    /// </summary>
    public static (int Number, IReadOnlyList<string> Letters)? ParseDisk(string instance)
    {
        var parts = instance.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return (number, parts.Skip(1).Where(part => part.EndsWith(':')).ToList());
    }

    /// <summary>
    /// LUID der Grafikkarte aus Instanznamen wie "pid_1234_luid_0x00000000_0x0000D1B2_phys_0_eng_0_engtype_3D"
    /// oder "luid_0x00000000_0x0000D1B2_phys_0". Ergebnis z. B. "0x00000000_0x0000D1B2".
    /// </summary>
    public static string? ParseGpuLuid(string instance)
    {
        var match = LuidPattern().Match(instance);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant().Replace("0X", "0x", StringComparison.Ordinal) : null;
    }

    /// <summary>
    /// GPU-Auslastung wie im Task-Manager: Die Werte aller Programme werden je Engine (3D, Kopieren, Video …) addiert;
    /// angezeigt wird die am stärksten ausgelastete Engine. Ergebnis je LUID, höchstens 100 %.
    /// </summary>
    public static IReadOnlyDictionary<string, double> AggregateGpuUsage(IEnumerable<KeyValuePair<string, double>> engineInstances)
    {
        var perEngine = new Dictionary<(string Luid, string Engine), double>();
        foreach (var (instance, value) in engineInstances)
        {
            var match = EnginePattern().Match(instance);
            if (!match.Success)
            {
                continue;
            }

            var key = (ParseGpuLuid(instance)!, match.Groups[1].Value);
            perEngine[key] = perEngine.GetValueOrDefault(key) + value;
        }

        return perEngine
            .GroupBy(entry => entry.Key.Luid)
            .ToDictionary(group => group.Key, group => Math.Min(group.Max(entry => entry.Value), 100));
    }

    /// <summary>Formatiert eine LUID so, wie sie in den Instanznamen steht (für den Abgleich mit der Grafikschnittstelle).</summary>
    public static string FormatLuid(int highPart, uint lowPart) =>
        string.Create(CultureInfo.InvariantCulture, $"0x{(uint)highPart:X8}_0x{lowPart:X8}");

    private static (int Group, int Index)? ParseCore(string instance)
    {
        var parts = instance.Split(',');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var group)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? (group, index)
            : null;
    }

    [GeneratedRegex(@"luid_(0x[0-9a-fA-F]{8}_0x[0-9a-fA-F]{8})")]
    private static partial Regex LuidPattern();

    // Engine = physische GPU + Engine-Nummer, z. B. "phys_0_eng_3"
    [GeneratedRegex(@"(phys_\d+_eng_\d+)")]
    private static partial Regex EnginePattern();
}
