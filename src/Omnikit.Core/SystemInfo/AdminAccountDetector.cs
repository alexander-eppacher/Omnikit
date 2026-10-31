using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Omnikit.Core.SystemInfo;

/// <summary>
/// Erkennt, ob das angemeldete Windows-Konto ein Administrator-Konto ist.
/// Hintergrund: Auch Admin-Konten starten Programme normalerweise OHNE Adminrechte (Benutzerkontensteuerung).
/// Windows merkt sich das als "Elevation Type" im Anmelde-Token:
///   Default (1) = Standardkonto, Full (2) = läuft schon als Admin, Limited (3) = Admin-Konto mit eingeschränkten Rechten.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class AdminAccountDetector
{
    private const int TokenElevationTypeClass = 18;
    private const int ElevationTypeDefault = 1;
    private const int ElevationTypeFull = 2;
    private const int ElevationTypeLimited = 3;

    public static bool? IsAdminAccount()
    {
        if (Environment.IsPrivilegedProcess)
        {
            return true;
        }

        using var identity = WindowsIdentity.GetCurrent();
        if (!GetTokenInformation(identity.Token, TokenElevationTypeClass, out var elevationType, sizeof(int), out _))
        {
            return null;
        }

        return elevationType switch
        {
            ElevationTypeFull or ElevationTypeLimited => true,
            ElevationTypeDefault => false,
            _ => null,
        };
    }

    // Windows-Funktion aus advapi32.dll: liest eine Information aus dem Anmelde-Token.
    [DllImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle, int tokenInformationClass, out int tokenInformation, int tokenInformationLength, out int returnLength);
}
