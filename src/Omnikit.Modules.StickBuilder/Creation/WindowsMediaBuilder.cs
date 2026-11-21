using System.ComponentModel;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Elevation;
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
    /// Gemerkte Installationsordner, dazu während des Baus die geprüfte Kopie der ESD und DISMs Arbeitsdateien.
    /// Unter „C:\Program Files“: Dort kann niemand ohne Adminrechte etwas anlegen – anders als unter ProgramData, wo jeder Benutzer
    /// den Ordner vorher anlegen oder durch eine Verknüpfung ersetzen könnte. Der Ordner selbst bekommt feste Rechte
    /// (nur Administratoren und System) und wird vor jeder Benutzung über ein Handle geprüft (<see cref="ProtectedFolder"/>).
    /// </summary>
    public static string CacheRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Omnikit Media");

    /// <summary>Früherer Ort des Caches (bis November 2026).</summary>
    private static string LegacyParent { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Omnikit");

    /// <summary>Platz für den Bau außer der ESD-Kopie: Setup-Dateien, boot.wim und install.wim (geteilt) – mit Reserve.</summary>
    private const long BuildSpaceBytes = 9_000_000_000;

    /// <summary>
    /// Liefert den fertigen Installationsordner zu dieser ESD; baut ihn nur, wenn er noch nicht gemerkt ist.
    /// Gebaut wird aus einer Kopie im geschützten Ordner, deren Prüfsumme beim Kopieren aus genau den geschriebenen Bytes
    /// berechnet wird: Die ESD liegt in einem Ordner des Benutzers und könnte nach einer Prüfung noch ausgetauscht werden.
    /// </summary>
    /// <exception cref="ElevatedOperationException">Ordner nicht vertrauenswürdig, zu wenig Platz oder falsche Prüfsumme.</exception>
    [SupportedOSPlatform("windows")]
    public static async Task<string> GetOrBuildAsync(string esdPath, string sha1, IProgress<string> progress, ILogger logger, CancellationToken cancellationToken)
    {
        if (!IsSha1(sha1))
        {
            throw new ArgumentException("Ungültige Prüfsumme.", nameof(sha1));
        }

        PrepareCacheRoot(logger);
        RemoveLegacyCache(logger);
        var media = Path.Combine(CacheRoot, sha1);
        if (IsDone(media, sha1))
        {
            return media; // schon fertig: nur noch kopieren
        }

        // Ältere Windows-Versionen und abgebrochene Versuche entfernen (je Version ca. 6 GB). Sicher, weil in diesem Baum
        // nur Administratoren und das System schreiben dürfen – dort kann niemand eine Verknüpfung untergeschoben haben.
        foreach (var entry in Directory.EnumerateFileSystemEntries(CacheRoot))
        {
            DeleteEntry(entry);
        }

        var esdSize = new FileInfo(esdPath).Length;
        if (new DriveInfo(Path.GetPathRoot(CacheRoot)!).AvailableFreeSpace < esdSize + BuildSpaceBytes)
        {
            throw new ElevatedOperationException(string.Format(CultureInfo.CurrentCulture, Texts.Create_NoSpace, (esdSize + BuildSpaceBytes) / 1e9));
        }

        var copy = Path.Combine(CacheRoot, sha1 + ".esd.tmp");
        var scratch = Path.Combine(CacheRoot, ScratchName);
        try
        {
            progress.Report(Texts.Create_Verifying);
            if (!string.Equals(await CopyWithHashAsync(esdPath, copy, cancellationToken), sha1, StringComparison.Ordinal))
            {
                throw new ElevatedOperationException(Texts.Create_HashMismatch);
            }

            Directory.CreateDirectory(scratch);
            var building = media + ".neu";
            await BuildFromEsdAsync(copy, building, scratch, progress, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(building, DoneMarker), sha1, cancellationToken);
            Directory.Move(building, media);
            return media;
        }
        finally
        {
            File.Delete(copy);
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
    }

    /// <summary>
    /// Arbeitsordner für DISM im geschützten Cache. Ohne Angabe nimmt DISM den Temp-Ordner des Benutzers, in den auch
    /// Programme ohne Adminrechte schreiben können.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static string PrepareScratch(ILogger logger)
    {
        PrepareCacheRoot(logger);
        var scratch = Path.Combine(CacheRoot, ScratchName);
        Directory.CreateDirectory(scratch);
        return scratch;
    }

    private const string ScratchName = "dism-arbeit";

    /// <summary>Genau 40 Hexadezimalzeichen in Kleinbuchstaben – sonst könnte ein Wert wie „..\x“ den Cache verlassen.</summary>
    internal static bool IsSha1(string value) => value.Length == 40 && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');

    /// <summary>Fertig ist ein Ordner nur, wenn die Marke da ist und genau diese Prüfsumme enthält.</summary>
    internal static bool IsDone(string media, string sha1)
    {
        var marker = Path.Combine(media, DoneMarker);
        return File.Exists(marker) && string.Equals(File.ReadAllText(marker).Trim(), sha1, StringComparison.Ordinal);
    }

    /// <summary>Kopiert die Datei und berechnet die SHA-1 aus den Bytes, die tatsächlich geschrieben werden.</summary>
    internal static async Task<string> CopyWithHashAsync(string source, string target, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
        return await CopyWithHashAsync(input, output, cancellationToken);
    }

    internal static async Task<string> CopyWithHashAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
#pragma warning disable CA5350 // SHA-1, weil Microsoft nur diese Prüfsumme liefert (siehe WindowsDownloader.Sha1Async)
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
#pragma warning restore CA5350
        var buffer = new byte[1 << 20];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    [SupportedOSPlatform("windows")]
    public static async Task BuildFromEsdAsync(string esdPath, string targetDirectory, string scratch, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var scratchDir = $"/ScratchDir:{scratch}";
        Directory.CreateDirectory(targetDirectory);
        var indexes = Dism.ParseIndexes(await Dism.RunAsync(["/Get-WimInfo", $"/WimFile:{esdPath}"], null, cancellationToken));
        if (indexes.Count < 4 || indexes[0] != 1)
        {
            throw new InvalidOperationException($"Unerwarteter Aufbau der ESD-Datei: {indexes.Count} Abbilder");
        }

        // 1. Setup-Dateien (Ordnerstruktur wie auf der ISO)
        await Dism.RunAsync(
            ["/Apply-Image", $"/ImageFile:{esdPath}", "/Index:1", $"/ApplyDir:{targetDirectory}", scratchDir],
            percent => Report(progress, Texts.Media_Setup, percent), cancellationToken);

        // 2. Startumgebung: Windows PE und Windows Setup in boot.wim; Setup muss startfähig sein
        var sources = Path.Combine(targetDirectory, "sources");
        Directory.CreateDirectory(sources);
        var bootWim = Path.Combine(sources, "boot.wim");
        await Dism.RunAsync(
            ["/Export-Image", $"/SourceImageFile:{esdPath}", "/SourceIndex:2", $"/DestinationImageFile:{bootWim}", "/Compress:max", "/CheckIntegrity", scratchDir],
            percent => Report(progress, Texts.Media_Boot, percent / 2), cancellationToken);
        await Dism.RunAsync(
            ["/Export-Image", $"/SourceImageFile:{esdPath}", "/SourceIndex:3", $"/DestinationImageFile:{bootWim}", "/Compress:max", "/CheckIntegrity", "/Bootable", scratchDir],
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
                    ["/Export-Image", $"/SourceImageFile:{esdPath}", $"/SourceIndex:{editions[i]}", $"/DestinationImageFile:{installWim}", "/Compress:max", "/CheckIntegrity", scratchDir],
                    percent => Report(progress, Texts.Media_Install, (step * 100 + percent) / editions.Count), cancellationToken);
            }

            await Dism.RunAsync(
                ["/Split-Image", $"/ImageFile:{installWim}", $"/SWMFile:{Path.Combine(sources, "install.swm")}", "/FileSize:3800", scratchDir],
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

    /// <exception cref="ElevatedOperationException">Der Cache-Ordner gibt es schon, aber nicht mit den erwarteten Rechten.</exception>
    [SupportedOSPlatform("windows")]
    private static void PrepareCacheRoot(ILogger logger)
    {
        var verdict = ProtectedFolder.Ensure(CacheRoot);
        if (verdict != FolderVerdict.Trusted)
        {
            logger.LogError("Cache-Ordner {Folder} nicht vertrauenswürdig: {Verdict}", CacheRoot, verdict);
            throw new ElevatedOperationException(string.Format(CultureInfo.CurrentCulture, Texts.Create_CacheUntrusted, CacheRoot));
        }
    }

    /// <summary>
    /// Löscht den Cache am früheren Ort (C:\ProgramData\Omnikit\media), aber nur, wenn beide Ebenen nachweislich nur für
    /// Administratoren und das System beschreibbar sind. Sonst bleibt er liegen (Hinweis im Log) – Löschen wäre dann gefährlicher als Platz.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void RemoveLegacyCache(ILogger logger)
    {
        var legacy = Path.Combine(LegacyParent, "media");
        if (!Directory.Exists(legacy))
        {
            return;
        }

        try
        {
            var parent = ProtectedFolder.Inspect(LegacyParent);
            var media = ProtectedFolder.Inspect(legacy);
            if (parent != FolderVerdict.Trusted || media != FolderVerdict.Trusted)
            {
                logger.LogWarning("Alter Cache {Folder} bleibt liegen ({Parent}/{Media}); bei Bedarf von Hand löschen", legacy, parent, media);
                return;
            }

            Directory.Delete(legacy, recursive: true);
            logger.LogInformation("Alter Cache {Folder} gelöscht", legacy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            logger.LogWarning(ex, "Alter Cache {Folder} konnte nicht gelöscht werden", legacy);
        }
    }

    private static void DeleteEntry(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static void Report(IProgress<string> progress, string format, double percent) =>
        progress.Report(string.Format(CultureInfo.CurrentCulture, format, (int)percent));

    [GeneratedRegex(@"^Edition\s*:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex EditionPattern();
}
