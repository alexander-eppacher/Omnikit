using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Elevation;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>
/// Erstellt einen bootfähigen USB-Stick aus einer ISO-Datei – wie Rufus. Läuft im Hilfsprozess mit Adminrechten,
/// nur nach Bestätigung durch den Nutzer. Parameter: Datenträgernummer, bestätigte Größe in Bytes, Gerätepfad, ob USB-Festplatten
/// erlaubt sind (0/1), Pfad der ISO – oder Pfad der von Microsoft geladenen Windows-Datei (.esd) und ihre SHA-1-Prüfsumme aus dem Katalog.
/// <list type="bullet">
/// <item>Windows-ISO: MBR, eine FAT32-Partition (höchstens 32 GB), alle Dateien kopieren; eine install.wim über 4 GB
/// wird mit DISM in Teile (.swm) zerlegt, weil FAT32 keine größeren Dateien kann.</item>
/// <item>Andere ISO (z. B. Linux): 1:1 auf den Stick schreiben („DD-Modus“).</item>
/// </list>
/// </summary>
public sealed class CreateStickOperation : IElevatedOperation
{
    public const string OperationId = "stick.create";

    private const long Fat32MaxFile = 4L * 1024 * 1024 * 1024 - 1;
    private const long MinStickForWindows = 7_000_000_000;
    private const int BufferSize = 4 * 1024 * 1024;

    /// <summary>Die geprüften Angaben aus der Befehlszeile des Hilfsprozesses.</summary>
    internal sealed record Request(int DiskNumber, long ConfirmedSize, string DevicePath, bool AllowFixed, string Path, string? Sha1);

    private readonly ILogger<CreateStickOperation> _logger;

    private readonly IDestructiveConfirmation _confirmation;

    public CreateStickOperation(ILogger<CreateStickOperation> logger, IDestructiveConfirmation confirmation)
    {
        _logger = logger;
        _confirmation = confirmation;
    }

    public string Id => OperationId;

    /// <summary>Ein Windows-Stick dauert je nach Stick 10–40 Minuten.</summary>
    public TimeSpan Timeout => TimeSpan.FromHours(2);

    /// <summary>Parameter für den Hilfsprozess. <paramref name="sha1"/> nur bei einer Windows-Datei (.esd) von Microsoft.</summary>
    /// <param name="allowFixed">Der Nutzer hat ausdrücklich auch USB-Festplatten zugelassen.</param>
    public static IReadOnlyList<string> Arguments(UsbDisk disk, bool allowFixed, string sourcePath, string? sha1 = null)
    {
        List<string> arguments =
        [
            disk.Number.ToString(CultureInfo.InvariantCulture), disk.SizeBytes.ToString(CultureInfo.InvariantCulture),
            disk.DevicePath, allowFixed ? "1" : "0", sourcePath,
        ];
        if (sha1 is not null)
        {
            arguments.Add(sha1);
        }

        return arguments;
    }

    public async Task<string> RunAsync(IReadOnlyList<string> arguments, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ElevatedOperationException(Texts.Create_WindowsOnly);
        }

        var request = ParseArguments(arguments);

        // Nie zwei Sticks gleichzeitig: Beide Läufe würden sich im Cache gegenseitig Ordner löschen und vielleicht denselben Stick bearbeiten.
        // Ein Semaphor statt Mutex, weil die Arbeit über mehrere Threads läuft. Nach einem Absturz gibt Windows ihn mit dem Prozess frei.
        using var running = new Semaphore(1, 1, @"Global\Omnikit.StickCreate");
        if (!running.WaitOne(TimeSpan.Zero))
        {
            throw new ElevatedOperationException(Texts.Create_AlreadyRunning);
        }

        try
        {
            return await RunCheckedAsync(request, progress, cancellationToken);
        }
        finally
        {
            running.Release();
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task<string> RunCheckedAsync(Request request, IProgress<string> progress, CancellationToken cancellationToken)
    {
        // Erste Prüfung sofort (verständliche Meldung, bevor etwas Langes passiert); die entscheidende kommt direkt vor dem Löschen
        var disk = UsbDiskFinder.Check(request.DevicePath, request.DiskNumber, request.ConfirmedSize, request.AllowFixed);
        var isoPath = request.Path;
        if (Environment.ProcessPath is { } self && UsbDiskFinder.HoldsFile(disk, self))
        {
            throw new ElevatedOperationException(Texts.Create_AppOnStick);
        }

        // Die ESD wird vor dem Löschen in den Cache kopiert und darf deshalb auf dem Stick liegen; eine ISO nicht
        if (request.Sha1 is null && UsbDiskFinder.HoldsFile(disk, isoPath))
        {
            throw new ElevatedOperationException(Texts.Create_SourceOnStick);
        }

        // Letzte Rückfrage im eigenen Fenster, mit dem Stick, wie ihn dieser Prozess selbst geprüft hat (nicht wie die App ihn nennt)
        if (!_confirmation.Confirm(Texts.Helper_ConfirmTitle, ConfirmText(disk, request)))
        {
            _logger.LogInformation("Löschen von Datenträger {Number} im Hilfsprozess abgelehnt", disk.Number);
            throw new ElevatedOperationException(Texts.Create_DeclinedInHelper);
        }

        if (request.Sha1 is { } sha1)
        {
            await CreateFromDownloadAsync(request, disk, sha1, progress, cancellationToken);
            return JsonSerializer.Serialize(new { mode = "windows" });
        }

        if (new FileInfo(isoPath).Length > disk.SizeBytes)
        {
            throw new ElevatedOperationException(Texts.Create_StickTooSmall);
        }

        _logger.LogInformation("Stick wird erstellt: Datenträger {Number} ({Name}, {Size} Bytes)", disk.Number, disk.Name, disk.SizeBytes);
        progress.Report(Texts.Create_Mounting);
        var source = await MountAsync(isoPath, cancellationToken);
        try
        {
            if (IsWindowsSetup(source))
            {
                await CreateWindowsStickAsync(request, source, progress, cancellationToken);
                return JsonSerializer.Serialize(new { mode = "windows" });
            }
        }
        finally
        {
            await PowerShellCommand.RunAsync($"Dismount-DiskImage -ImagePath {PowerShellCommand.Quote(isoPath)} | Out-Null", CancellationToken.None);
        }

        await WriteRawAsync(request, isoPath, progress, cancellationToken);
        return JsonSerializer.Serialize(new { mode = "raw" });
    }

    /// <summary>Text der letzten Rückfrage: welcher Datenträger (Name, Größe, Nummer) und was darauf geschrieben wird.</summary>
    internal static string ConfirmText(UsbDisk disk, Request request)
    {
        var source = request.Sha1 is null ? Path.GetFileName(request.Path) : Texts.Helper_ConfirmWindows;
        var text = string.Format(CultureInfo.CurrentCulture, Texts.Helper_Confirm, disk.DisplayName, disk.Number, source);
        return disk.IsRemovable ? text : Texts.Confirm_FixedWarning + Environment.NewLine + Environment.NewLine + text;
    }

    /// <summary>
    /// Strenge Prüfung der Parameter: Zahlen, ein Gerätepfad von Windows, 0 oder 1, ein vorhandener absoluter Pfad zu einer .iso-Datei –
    /// oder zu einer .esd-Datei mit Prüfsumme (40 Hexadezimalzeichen).
    /// </summary>
    internal static Request ParseArguments(IReadOnlyList<string> arguments)
    {
        var isEsd = arguments.Count == 6;
        if (arguments.Count is not (5 or 6)
            || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > 99
            || !long.TryParse(arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size <= 0
            || !UsbDiskFinder.IsDevicePath(arguments[2])
            || arguments[3] is not ("0" or "1")
            || !Path.IsPathFullyQualified(arguments[4])
            || !arguments[4].EndsWith(isEsd ? ".esd" : ".iso", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(arguments[4])
            || (isEsd && (arguments[5].Length != 40 || !arguments[5].All(char.IsAsciiHexDigit))))
        {
            throw new ElevatedOperationException(Texts.Create_BadArguments);
        }

        return new Request(number, size, arguments[2], arguments[3] == "1", arguments[4], isEsd ? arguments[5].ToLowerInvariant() : null);
    }

    /// <summary>
    /// Windows von Microsoft: den Installationsordner holen (beim ersten Mal aus einer geprüften Kopie der Datei gebaut,
    /// danach gemerkt) und auf den Stick kopieren. Die Prüfsumme prüft <see cref="WindowsMediaBuilder"/> beim Kopieren.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private async Task CreateFromDownloadAsync(Request request, UsbDisk disk, string sha1, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (disk.SizeBytes < MinStickForWindows)
        {
            throw new ElevatedOperationException(Texts.Create_StickTooSmall);
        }

        var media = await WindowsMediaBuilder.GetOrBuildAsync(request.Path, sha1, progress, _logger, cancellationToken);
        await CreateWindowsStickAsync(request, media, progress, cancellationToken);
    }

    /// <summary>Windows-Installationsmedium: Startdatei für UEFI und ein Windows-Abbild.</summary>
    internal static bool IsWindowsSetup(string root) =>
        File.Exists(Path.Combine(root, "setup.exe"))
        && (File.Exists(Path.Combine(root, "sources", "install.wim")) || File.Exists(Path.Combine(root, "sources", "install.esd")));

    [SupportedOSPlatform("windows")]
    private async Task CreateWindowsStickAsync(Request request, string source, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var esd = Path.Combine(source, "sources", "install.esd");
        if (File.Exists(esd) && new FileInfo(esd).Length > Fat32MaxFile)
        {
            throw new ElevatedOperationException(Texts.Create_EsdTooLarge);
        }

        progress.Report(Texts.Create_Formatting);
        var target = await PrepareFat32Async(CheckAgain(request), request.AllowFixed, cancellationToken);

        var wim = Path.Combine(source, "sources", "install.wim");
        var splitWim = File.Exists(wim) && new FileInfo(wim).Length > Fat32MaxFile;
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(file => !(splitWim && string.Equals(file, wim, StringComparison.OrdinalIgnoreCase)))
            .Where(file => !string.Equals(Path.GetFileName(file), WindowsMediaBuilder.DoneMarker, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await CopyAsync(source, target, files, progress, cancellationToken);

        if (splitWim)
        {
            await Dism.RunAsync(
                ["/Split-Image", $"/ImageFile:{wim}", $"/SWMFile:{Path.Combine(target, "sources", "install.swm")}", "/FileSize:3800",
                    $"/ScratchDir:{WindowsMediaBuilder.PrepareScratch(_logger)}"],
                percent => progress.Report(string.Format(CultureInfo.CurrentCulture, Texts.Create_SplittingPercent, (int)percent)), cancellationToken);
        }

        _logger.LogInformation("Windows-Stick fertig auf {Target}", target);
    }

    /// <summary>
    /// Nach dem langen Teil (Prüfsumme, DISM, ISO einbinden) noch einmal prüfen – mit verständlicher Meldung.
    /// Die letzte, entscheidende Prüfung macht <see cref="EraseScript"/> im selben Aufruf wie das Löschen.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static UsbDisk CheckAgain(Request request) =>
        UsbDiskFinder.Check(request.DevicePath, request.DiskNumber, request.ConfirmedSize, request.AllowFixed);

    /// <summary>Stick leeren, MBR, eine aktive FAT32-Partition (Windows formatiert FAT32 nur bis 32 GB).</summary>
    [SupportedOSPlatform("windows")]
    private static async Task<string> PrepareFat32Async(UsbDisk disk, bool allowFixed, CancellationToken cancellationToken)
    {
        var letter = await EraseScript.RunAsync(disk, allowFixed, FormatFat32Script, cancellationToken);
        return letter.Length == 1 && char.IsAsciiLetter(letter[0])
            ? letter + @":\"
            : throw new InvalidOperationException($"Unerwarteter Laufwerksbuchstabe: {letter}");
    }

    /// <summary>Läuft direkt nach <see cref="EraseScript"/>; <c>$d</c> ist der geprüfte, leere Stick.</summary>
    internal const string FormatFat32Script = """
        # MBR: Nur dort gibt es eine „aktive“ Partition. Clear-Disk lässt GPT-Sticks manchmal GPT, also umstellen.
        if ($d.PartitionStyle -eq 'RAW') { $d | Initialize-Disk -PartitionStyle MBR }
        elseif ($d.PartitionStyle -ne 'MBR') { $d | Set-Disk -PartitionStyle MBR }
        $d = Get-Disk -Path $env:OMNIKIT_DISK_PATH
        $size = [Math]::Min($d.Size - 8MB, 32GB - 8MB)
        $partition = $d | New-Partition -Size $size -IsActive -AssignDriveLetter
        Format-Volume -Partition $partition -FileSystem FAT32 -NewFileSystemLabel 'OMNIKIT' -Confirm:$false -Force | Out-Null
        ($d | Get-Partition | Where-Object PartitionNumber -eq $partition.PartitionNumber).DriveLetter
        """;

    [SupportedOSPlatform("windows")]
    private static async Task<string> MountAsync(string isoPath, CancellationToken cancellationToken)
    {
        var letter = await PowerShellCommand.RunAsync(
            $"(Mount-DiskImage -ImagePath {PowerShellCommand.Quote(isoPath)} -StorageType ISO -PassThru | Get-Volume).DriveLetter",
            cancellationToken);
        return letter.Length == 1 && char.IsAsciiLetter(letter[0])
            ? letter + @":\"
            : throw new ElevatedOperationException(Texts.Create_MountFailed);
    }

    /// <summary>Kopiert alle Dateien und meldet den Fortschritt in ganzen Prozent.</summary>
    private static async Task CopyAsync(string source, string target, IReadOnlyList<string> files, IProgress<string> progress, CancellationToken cancellationToken)
    {
        var total = Math.Max(1, files.Sum(file => new FileInfo(file).Length));
        long copied = 0;
        var lastPercent = -1;
        var buffer = new byte[BufferSize];

        foreach (var file in files)
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copied += read;
                var percent = (int)(copied * 100 / total);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(string.Format(CultureInfo.CurrentCulture, Texts.Create_Copying, percent));
                }
            }
        }
    }

    /// <summary>„DD-Modus“: die ISO Sektor für Sektor auf den Datenträger schreiben.</summary>
    [SupportedOSPlatform("windows")]
    private async Task WriteRawAsync(Request request, string isoPath, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report(Texts.Create_Formatting);
        var disk = CheckAgain(request);
        // Erst löschen (danach gibt es keine eingehängten Partitionen mehr, die das Schreiben blockieren), dann über den Gerätepfad öffnen
        await EraseScript.RunAsync(disk, request.AllowFixed, string.Empty, cancellationToken);
        using (var target = RawDisk.OpenVerified(disk, request.AllowFixed))
        await using (var input = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true))
        {
            await RawDisk.WriteImageAsync(
                input,
                input.Length,
                (data, offset, ct) => RandomAccess.WriteAsync(target, data, offset, ct),
                percent => progress.Report(string.Format(CultureInfo.CurrentCulture, Texts.Create_Writing, percent)),
                BufferSize,
                cancellationToken);

            // Windows soll den neuen Inhalt erkennen
            RawDisk.UpdateProperties(target);
        }

        _logger.LogInformation("ISO 1:1 auf Datenträger {Number} geschrieben", disk.Number);
    }
}
