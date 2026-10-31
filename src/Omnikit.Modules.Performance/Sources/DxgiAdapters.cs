using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Omnikit.Modules.Performance.Data;
using Omnikit.Modules.Performance.Monitoring;

namespace Omnikit.Modules.Performance.Sources;

/// <summary>
/// Liest die Grafikkarten über DXGI (DirectX Graphics Infrastructure, Teil von Windows): Name, Grafikspeicher und LUID.
/// Über die LUID lassen sich die Leistungsindikatoren („GPU Engine“) der richtigen Grafikkarte zuordnen,
/// auch bei Laptops mit zwei Grafikchips.
/// </summary>
/// <remarks>
/// DXGI ist eine COM-Schnittstelle. Statt einer Zusatzbibliothek rufen wir die zwei benötigten Funktionen
/// direkt über ihre Position in der Funktionstabelle (vtable) auf. Die Positionen stehen in den Windows-Headern (dxgi.h).
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class DxgiAdapters
{
    private static readonly Guid FactoryInterface = new("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
    private const int NotFound = unchecked((int)0x887A0002); // DXGI_ERROR_NOT_FOUND: keine weiteren Adapter
    private const uint SoftwareAdapter = 2;                   // "Microsoft Basic Render Driver" – keine echte GPU

    // Positionen in der Funktionstabelle laut dxgi.h
    private const int ReleaseSlot = 2;        // IUnknown::Release
    private const int EnumAdapters1Slot = 12; // IDXGIFactory1::EnumAdapters1
    private const int GetDesc1Slot = 10;      // IDXGIAdapter1::GetDesc1

    public static List<GpuInfo> Read()
    {
        var result = new List<GpuInfo>();
        if (CreateDXGIFactory1(FactoryInterface, out var factory) < 0)
        {
            return result;
        }

        try
        {
            var enumAdapters = Method<EnumAdapters1Fn>(factory, EnumAdapters1Slot);
            for (uint index = 0; enumAdapters(factory, index, out var adapter) != NotFound; index++)
            {
                if (adapter == IntPtr.Zero)
                {
                    break;
                }

                try
                {
                    if (Method<GetDesc1Fn>(adapter, GetDesc1Slot)(adapter, out var desc) >= 0 && (desc.Flags & SoftwareAdapter) == 0)
                    {
                        result.Add(new GpuInfo(
                            CounterInstances.FormatLuid(desc.LuidHighPart, desc.LuidLowPart),
                            desc.Description.Trim(),
                            (long)desc.DedicatedVideoMemory,
                            (long)desc.SharedSystemMemory));
                    }
                }
                finally
                {
                    Release(adapter);
                }
            }
        }
        finally
        {
            Release(factory);
        }

        // Dieselbe Karte kann mehrfach erscheinen (eine pro angeschlossenem Bildschirmausgang)
        return result.DistinctBy(gpu => gpu.Luid).ToList();
    }

    private static T Method<T>(IntPtr comObject, int slot)
        where T : Delegate
    {
        var table = Marshal.ReadIntPtr(comObject);
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table, slot * IntPtr.Size));
    }

    private static void Release(IntPtr comObject) => Method<ReleaseFn>(comObject, ReleaseSlot)(comObject);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Fn(IntPtr factory, uint index, out IntPtr adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1Fn(IntPtr adapter, out AdapterDesc1 desc);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFn(IntPtr comObject);

    // DXGI_ADAPTER_DESC1 aus dxgi.h
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public uint LuidLowPart;
        public int LuidHighPart;
        public uint Flags;
    }

    [DllImport("dxgi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CreateDXGIFactory1(in Guid riid, out IntPtr factory);
}
