using System.Globalization;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>
/// Baut aus der Windows-Datei von Microsoft (ESD) einen fertigen Installationsordner – wie das Media Creation Tool:
/// Abbild 1 = Setup-Dateien, 2 und 3 = Startumgebung (boot.wim), dazu die gängigen Editionen (install.wim, für FAT32 geteilt).
/// Das Ergebnis wird in einem geschützten Ordner gemerkt: Jeder weitere Stick mit derselben Windows-Version wird nur noch kopiert.
/// </summary>
public static partial class WindowsMediaBuilder
{
    /// <summary>Nur diese Editionen kommen auf den Stick (N-, Workstation- und Einzelsprach-Versionen braucht man praktisch nie).</summary>
    internal static readonly string[] Editions = ["Core", "Professional", "Education", "ProfessionalEducation"];

    /// <summary>Wird zuletzt geschrieben: Nur ein vollständig gebauter Ordner gilt als fertig. Kommt nicht auf den Stick.</summary>
    internal const string DoneMarker = "omnikit-fertig.txt";

    /// <summary>
    /// Gemerkte Installationsordner. Unter ProgramData mit festen Rechten: Schreiben dürfen nur Administratoren und das System,
    /// damit niemand ohne Adminrechte die Dateien austauschen kann, die später mit Adminrechten auf den Stick kommen.
    /// </summary>
    public static string CacheRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Omnikit", "media");

    /// <summary>Liefert den fertigen Installationsordner zu dieser ESD; baut ihn nur, wenn er noch nicht gemerkt ist.</summary>
    [SupportedOSPlatform("windows")]
    public static async Task<string> GetOrBuildAsync(string esdPath, string sha1, IProgress<string> progress, CancellationToken cancellationToken)
    {
        PrepareCacheRoot();
        var media = Path.Combine(CacheRoot, sha1);
        if (File.Exists(Path.Combine(media, DoneMarker)))
        {
            return media; // schon fertig: nur noch kopieren
        }

        // Ältere Windows-Versionen und abgebrochene Versuche entfernen (je Version ca. 6 GB)
        foreach (var folder in Directory.GetDirectories(CacheRoot))
        {
            Directory.Delete(folder, recursive: true);
        }

        var building = media + ".neu";
        await BuildFromEsdAsync(esdPath, building, progress, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(building, DoneMarker), sha1, cancellationToken);
        Directory.Move(building, media);
        return media;
    }

    [SupportedOSPlatform("windows")]
    public static async Task BuildFromEsdAsync(string esdPath, string targetDirectory, IProgress<string> progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);
        var indexes = Dism.ParseIndexes(await Dism.RunAsync(["/Get-WimInfo", $"/WimFile:{esdPath}"], null, cancellationToken));
        if (indexes.Count < 4 || indexes[0] != 1)
        {
            throw new InvalidOperationException($"Unerwarteter Aufbau der ESD-Datei: {indexes.Count} Abbilder");
        }

        // 1. Setup-Dateien (Ordnerstruktur wie auf der ISO)
        await Dism.RunAsync(
            ["/Apply-Image", $"/ImageFile:{esdPath}", "/Index:1", $"/ApplyDir:{targetDirectory}"],
            percent => Report(progress, Texts.Media_Setup, percent), cancellationToken);

        // 2. Startumgebung: Windows PE und Windows Setup in boot.wim; Setup muss startfähig sein
        var sources = Path.Combine(targetDirectory, "sources");
        Directory.CreateDirectory(sources);
        var bootWim = Path.Combine(sources, "boot.wim");
        await Dism.RunAsync(
            ["/Export-Image", $"/SourceImageFile:{esdPath}", "/SourceIndex:2", $"/DestinationImageFile:{bootWim}", "/Compress:max", "/CheckIntegrity"],
            percent => Report(progress, Texts.Media_Boot, percent / 2), cancellationToken);
        await Dism.RunAsync(
            ["/Export-Image", $"/SourceImageFile:{esdPath}", "/SourceIndex:3", $"/DestinationImageFile:{bootWim}", "/Compress:max", "/CheckIntegrity", "/Bootable"],
            percent => Report(progress, Texts.Media_Boot, 50 + percent / 2), cancellationToken);

        // 3. Die gängigen Editionen in eine install.wim neben dem Zielordner; danach für FAT32 in Stücke unter 4 GB teilen
        var editions = new List<int>();
        foreach (var index in indexes.Where(index => index >= 4))
        {
            var details = await Dism.RunAsync(["/Get-WimInfo", $"/WimFile:{esdPath}", $"/Index:{index}"], null, cancellationToken);
            if (ParseEdition(details) is { } edition && Editions.Contains(edition, StringComparer.OrdinalIgnoreCase))
            {
                editions.Add(index);
            }
        }

        if (editions.Count == 0)
        {
            throw new InvalidOperationException("Keine passende Windows-Edition in der ESD-Datei gefunden.");
        }

        var installWim = targetDirectory + ".install.wim";
        try
        {
            for (var i = 0; i < editions.Count; i++)
            {
                var step = i;
                await Dism.RunAsync(
                    ["/Export-Image", $"/SourceImageFile:{esdPath}", $"/SourceIndex:{editions[i]}", $"/DestinationImageFile:{installWim}", "/Compress:max", "/CheckIntegrity"],
                    percent => Report(progress, Texts.Media_Install, (step * 100 + percent) / editions.Count), cancellationToken);
            }

            await Dism.RunAsync(
                ["/Split-Image", $"/ImageFile:{installWim}", $"/SWMFile:{Path.Combine(sources, "install.swm")}", "/FileSize:3800"],
                percent => Report(progress, Texts.Create_SplittingPercent, percent), cancellationToken);
        }
        finally
        {
            File.Delete(installWim);
        }
    }

    /// <summary>Liest „Edition : Professional“ aus /Get-WimInfo /Index:n.</summary>
    internal static string? ParseEdition(string getWimInfoOutput) =>
        EditionPattern().Match(getWimInfoOutput) is { Success: true } match ? match.Groups[1].Value.Trim() : null;

    /// <summary>
    /// Legt den Ordner mit festen Rechten an (Administratoren und System: Vollzugriff, Benutzer: nur lesen).
    /// Existiert er schon, gehört aber nicht den Administratoren oder dem System, wird er verworfen –
    /// jemand ohne Adminrechte könnte ihn vorher angelegt und Dateien hineingelegt haben.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void PrepareCacheRoot()
    {
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        var root = new DirectoryInfo(CacheRoot);
        if (root.Exists && root.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) is var owner && owner != administrators && owner != system)
        {
            root.Delete(recursive: true);
            root.Refresh();
        }

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(administrators);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));

        if (root.Exists)
        {
            root.SetAccessControl(security);
        }
        else
        {
            root.Create(security);
        }
    }

    private static void Report(IProgress<string> progress, string format, double percent) =>
        progress.Report(string.Format(CultureInfo.CurrentCulture, format, (int)percent));

    [GeneratedRegex(@"^Edition\s*:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex EditionPattern();
}
