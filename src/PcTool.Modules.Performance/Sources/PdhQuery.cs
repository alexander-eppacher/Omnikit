using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PcTool.Modules.Performance.Sources;

/// <summary>
/// Eine offene Abfrage von Windows-Leistungsindikatoren (pdh.dll, „Performance Data Helper“) – dieselbe Quelle wie im Task-Manager.
/// Werte wie „Prozent“ oder „pro Sekunde“ entstehen aus zwei Messungen; deshalb bleibt die Abfrage offen
/// und <see cref="Collect"/> wird jede Sekunde aufgerufen.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class PdhQuery : IDisposable
{
    private const uint Success = 0;
    private const uint MoreData = 0x800007D2;
    private const uint FormatDouble = 0x00000200;
    private const uint FormatNoCap100 = 0x00008000; // Prozentwerte nicht bei 100 abschneiden; wir begrenzen selbst

    private IntPtr _query;

    private PdhQuery(IntPtr query)
    {
        _query = query;
    }

    public static PdhQuery? TryOpen() =>
        PdhOpenQueryW(null, IntPtr.Zero, out var query) == Success ? new PdhQuery(query) : null;

    /// <summary>
    /// Fügt einen Indikator mit englischem Namen hinzu (funktioniert auch auf deutschem Windows).
    /// <c>null</c>, wenn es ihn auf diesem System nicht gibt (z. B. GPU-Indikatoren in einer VM ohne Grafikkarte).
    /// </summary>
    public IntPtr? TryAdd(string englishPath) =>
        PdhAddEnglishCounterW(_query, englishPath, IntPtr.Zero, out var counter) == Success ? counter : null;

    public bool Collect() => PdhCollectQueryData(_query) == Success;

    /// <summary>Wert eines Indikators ohne Platzhalter, z. B. "\Processor Information(_Total)\% Processor Utility".</summary>
    public static double? GetValue(IntPtr? counter)
    {
        if (counter is not IntPtr handle
            || PdhGetFormattedCounterValue(handle, FormatDouble | FormatNoCap100, out _, out var value) != Success
            || !IsValid(value.Status))
        {
            return null;
        }

        return value.DoubleValue;
    }

    /// <summary>Alle Instanzen eines Indikators mit Platzhalter "(*)", z. B. je Kern oder je Laufwerk.</summary>
    public static IReadOnlyList<KeyValuePair<string, double>> GetValues(IntPtr? counter)
    {
        if (counter is not IntPtr handle)
        {
            return [];
        }

        // Erst fragen, wie viel Platz nötig ist, dann mit passendem Puffer abholen.
        uint bufferSize = 0;
        if (PdhGetFormattedCounterArrayW(handle, FormatDouble | FormatNoCap100, ref bufferSize, out _, IntPtr.Zero) != MoreData)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            if (PdhGetFormattedCounterArrayW(handle, FormatDouble | FormatNoCap100, ref bufferSize, out var count, buffer) != Success)
            {
                return [];
            }

            var result = new List<KeyValuePair<string, double>>((int)count);
            var itemSize = Marshal.SizeOf<CounterValueItem>();
            for (var i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<CounterValueItem>(buffer + (i * itemSize));
                if (IsValid(item.Value.Status) && Marshal.PtrToStringUni(item.Name) is string name)
                {
                    result.Add(new KeyValuePair<string, double>(name, item.Value.DoubleValue));
                }
            }

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            _ = PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    // 0 = gültig, 1 = gültig und neu
    private static bool IsValid(uint status) => status is 0 or 1;

    // Ergebnis einer Messung: Status und der Wert (hier als Kommazahl).
    [StructLayout(LayoutKind.Explicit)]
    private struct CounterValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
    }

    // Ein Eintrag der Liste bei Platzhaltern: Instanzname und Wert.
    [StructLayout(LayoutKind.Sequential)]
    private struct CounterValueItem
    {
        public IntPtr Name;
        public CounterValue Value;
    }

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

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PdhCloseQuery(IntPtr query);
}
