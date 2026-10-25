using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PcTool.Modules.Health.Sources;

/// <summary>
/// Misst die CPU-Auslastung so wie der Task-Manager: Leistungsindikator "% Processor Utility".
/// Er berücksichtigt auch die Taktfrequenz (Turbo), deshalb weicht der ältere Wert
/// Win32_Processor.LoadPercentage oft deutlich vom Task-Manager ab.
/// Ein Leistungsindikator braucht zwei Messungen; dazwischen wird eine Sekunde gewartet.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ProcessorUtilityCounter
{
    // Englischer Name; PdhAddEnglishCounter findet ihn auch auf deutschem Windows.
    private const string CounterPath = @"\Processor Information(_Total)\% Processor Utility";
    private const uint FormatDouble = 0x00000200;
    private const uint Success = 0;

    /// <summary>Auslastung in Prozent (0–100) oder null, wenn der Indikator nicht verfügbar ist.</summary>
    public static int? Measure(TimeSpan interval)
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out var query) != Success)
        {
            return null;
        }

        try
        {
            if (PdhAddEnglishCounterW(query, CounterPath, IntPtr.Zero, out var counter) != Success
                || PdhCollectQueryData(query) != Success)
            {
                return null;
            }

            Thread.Sleep(interval);

            if (PdhCollectQueryData(query) != Success
                || PdhGetFormattedCounterValue(counter, FormatDouble, out _, out var value) != Success)
            {
                return null;
            }

            return (int)Math.Round(Math.Clamp(value.DoubleValue, 0, 100));
        }
        finally
        {
            _ = PdhCloseQuery(query);
        }
    }

    // Ergebnis von PdhGetFormattedCounterValue: Status und der Wert (hier als Kommazahl).
    [StructLayout(LayoutKind.Explicit)]
    private struct CounterValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
    }

    // Windows-Funktionen aus pdh.dll (Performance Data Helper), die auch der Task-Manager nutzt.
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string counterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out CounterValue value);

    [DllImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhCloseQuery(IntPtr query);
}
