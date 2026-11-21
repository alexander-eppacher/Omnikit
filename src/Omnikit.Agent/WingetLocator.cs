using System.Security.Principal;
using Omnikit.Modules.Apps.Winget;

namespace Omnikit.Agent;

/// <summary>
/// Findet winget.exe. Als Dienst (SYSTEM) gibt es den Aufruf im Benutzerprofil nicht; dann wird winget direkt aus
/// dem Installationsordner des App-Installers gestartet (neueste Version), zusammen mit dem Ordner der
/// Visual-C++-Laufzeit („VCLibs“), die winget dort sonst nicht findet. Beide Ordner gehören Windows – dort kann
/// niemand eine fremde winget.exe oder DLL ablegen. Auf der Test-VM geprüft (Entscheidung 0016).
/// </summary>
public static class WingetLocator
{
    private const string AppInstallerPattern = "Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe";
    private const string VcLibsPattern = "Microsoft.VCLibs.140.00.UWPDesktop_*_x64__8wekyb3d8bbwe";

    public static WingetLocation? Find()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var identity = WindowsIdentity.GetCurrent();
        if (!identity.IsSystem && File.Exists(WingetRunner.UserWingetPath))
        {
            return new WingetLocation(WingetRunner.UserWingetPath, []);
        }

        var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
        try
        {
            if (Newest(Directory.GetDirectories(windowsApps, AppInstallerPattern)) is not string appInstaller
                || !File.Exists(Path.Combine(appInstaller, "winget.exe")))
            {
                return null;
            }

            var vcLibs = Newest(Directory.GetDirectories(windowsApps, VcLibsPattern));
            return new WingetLocation(Path.Combine(appInstaller, "winget.exe"), vcLibs is null ? [appInstaller] : [vcLibs, appInstaller]);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>Ordner mit der höchsten Versionsnummer, z. B. „…_1.26.430.0_x64__…“.</summary>
    internal static string? Newest(IEnumerable<string> folders) =>
        folders
            .Select(folder => (Folder: folder, Version: Version.TryParse(Path.GetFileName(folder).Split('_').ElementAtOrDefault(1), out var version) ? version : null))
            .Where(entry => entry.Version is not null)
            .OrderByDescending(entry => entry.Version)
            .Select(entry => entry.Folder)
            .FirstOrDefault();
}
