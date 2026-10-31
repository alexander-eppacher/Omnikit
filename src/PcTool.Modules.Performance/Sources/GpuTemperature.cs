using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PcTool.Modules.Performance.Sources;

/// <summary>
/// Versucht, die GPU-Temperatur über die Grafikschnittstelle von Windows (D3DKMT in gdi32.dll) zu lesen –
/// dieselbe Quelle wie der Task-Manager. Klappt nur bei Grafikkarten, deren Treiber den Wert meldet
/// (meist eigene Grafikkarten, selten Prozessorgrafik). Ohne Wert wird nichts angezeigt, nie geraten.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class GpuTemperature
{
    // Abfragetyp "Leistungsdaten des Grafikadapters" (KMTQAITYPE_ADAPTERPERFDATA)
    private const int QueryAdapterPerfData = 62;

    // Grafikkarten, bei denen die Abfrage einmal fehlgeschlagen ist, werden nicht ständig neu gefragt.
    private static readonly ConcurrentDictionary<string, bool> Unsupported = new();

    /// <param name="luid">Format wie in den Leistungsindikatoren, z. B. "0x00000000_0x0000D1B2".</param>
    public static double? TryRead(string luid)
    {
        if (Unsupported.ContainsKey(luid) || ParseLuid(luid) is not var (high, low))
        {
            return null;
        }

        var open = new OpenAdapterFromLuid { LowPart = low, HighPart = high };
        if (D3DKMTOpenAdapterFromLuid(ref open) != 0)
        {
            Unsupported[luid] = true;
            return null;
        }

        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<AdapterPerfData>());
        try
        {
            Marshal.StructureToPtr(new AdapterPerfData(), buffer, false);
            var query = new QueryAdapterInfo
            {
                Adapter = open.Adapter,
                Type = QueryAdapterPerfData,
                PrivateData = buffer,
                PrivateDataSize = (uint)Marshal.SizeOf<AdapterPerfData>(),
            };

            if (D3DKMTQueryAdapterInfo(ref query) != 0)
            {
                Unsupported[luid] = true;
                return null;
            }

            // Windows meldet Zehntelgrad. 0 heißt „kein Sensor“; unplausible Werte verwerfen statt anzeigen.
            var celsius = Marshal.PtrToStructure<AdapterPerfData>(buffer).Temperature / 10.0;
            return celsius is >= 10 and <= 125 ? celsius : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            var close = new CloseAdapter { Adapter = open.Adapter };
            _ = D3DKMTCloseAdapter(ref close);
        }
    }

    private static (int High, uint Low)? ParseLuid(string luid)
    {
        var parts = luid.Split('_');
        return parts.Length == 2
            && uint.TryParse(parts[0].AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var high)
            && uint.TryParse(parts[1].AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var low)
            ? ((int)high, low)
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenAdapterFromLuid
    {
        public uint LowPart;
        public int HighPart;
        public uint Adapter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public IntPtr PrivateData;
        public uint PrivateDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterPerfData
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOverclocked;
        public ulong MemoryBandwidth;
        public ulong PcieBandwidth;
        public uint FanRpm;
        public uint Power;
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CloseAdapter
    {
        public uint Adapter;
    }

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int D3DKMTOpenAdapterFromLuid(ref OpenAdapterFromLuid openAdapter);

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo queryInfo);

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int D3DKMTCloseAdapter(ref CloseAdapter closeAdapter);
}
