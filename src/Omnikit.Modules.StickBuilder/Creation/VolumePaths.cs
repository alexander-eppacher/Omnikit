using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>Wo eine Datei wirklich liegt: Windows löst dafür Laufwerksbuchstaben, <c>subst</c> und Verknüpfungen auf.</summary>
[SupportedOSPlatform("windows")]
internal static class VolumePaths
{
    private const uint FileNameNormalized = 0x0;
    private const uint VolumeNameGuid = 0x1;

    /// <summary>Pfad der geöffneten Datei mit Volume-Kennung, z. B. <c>\\?\Volume{1234…}\ISOs\ubuntu.iso</c>.</summary>
    public static string FinalPath(string file)
    {
        using var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new char[1024];
        var length = GetFinalPathNameByHandleW(handle, buffer, buffer.Length, FileNameNormalized | VolumeNameGuid);
        if (length == 0 || length >= buffer.Length)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new string(buffer, 0, length);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetFinalPathNameByHandleW(SafeFileHandle file, [Out] char[] path, int length, uint flags);
}
