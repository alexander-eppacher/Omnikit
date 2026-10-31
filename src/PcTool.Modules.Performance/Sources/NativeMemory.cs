using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using PcTool.Modules.Performance.Data;

namespace PcTool.Modules.Performance.Sources;

/// <summary>
/// Arbeitsspeicher und Systemzähler direkt von Windows (kernel32.dll). Sehr schnell, deshalb jede Sekunde abfragbar.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NativeMemory
{
    /// <summary>Belegung wie im Task-Manager: „In Verwendung“ = Gesamt − Verfügbar; „Zugesichert“ aus den Seiten-Zählern.</summary>
    public static (MemorySample? Memory, SystemCounts? Counts) Read()
    {
        var info = new PerformanceInformation { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (!GetPerformanceInfo(ref info, info.Size))
        {
            return (null, null);
        }

        var page = (long)info.PageSize;
        var memory = new MemorySample(
            (long)info.PhysicalTotal * page,
            (long)info.PhysicalAvailable * page,
            (long)info.CommitTotal * page,
            (long)info.CommitLimit * page);
        var counts = new SystemCounts((int)info.ProcessCount, (int)info.ThreadCount, (int)info.HandleCount);
        return (memory, counts);
    }

    /// <summary>Eingebauter Arbeitsspeicher laut Firmware (mehr als nutzbar, wenn die Grafik einen Teil reserviert).</summary>
    public static long? InstalledBytes() =>
        GetPhysicallyInstalledSystemMemory(out var kilobytes) ? (long)kilobytes * 1024 : null;

    // Seitenangaben (Anzahl Speicherseiten); mal PageSize ergibt Bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public UIntPtr CommitTotal;
        public UIntPtr CommitLimit;
        public UIntPtr CommitPeak;
        public UIntPtr PhysicalTotal;
        public UIntPtr PhysicalAvailable;
        public UIntPtr SystemCache;
        public UIntPtr KernelTotal;
        public UIntPtr KernelPaged;
        public UIntPtr KernelNonpaged;
        public UIntPtr PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("kernel32.dll", EntryPoint = "K32GetPerformanceInfo", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(ref PerformanceInformation information, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
}
