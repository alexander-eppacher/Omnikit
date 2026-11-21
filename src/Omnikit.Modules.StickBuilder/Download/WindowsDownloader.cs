using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Omnikit.Core;

namespace Omnikit.Modules.StickBuilder.Download;

/// <summary>Fortschritt beim Herunterladen.</summary>
public sealed record DownloadProgress(long BytesDone, long BytesTotal, bool IsVerifying = false)
{
    public int Percent => BytesTotal > 0 ? (int)(BytesDone * 100 / BytesTotal) : 0;
}

/// <summary>
/// Lädt Windows direkt von Microsoft – so wie das Media Creation Tool. Die Dateien selbst gibt es nur per HTTP;
/// deshalb wird jede Datei gegen die Prüfsumme aus dem (per HTTPS geladenen) Katalog geprüft, bevor sie verwendet wird.
/// Geladene Dateien bleiben in <c>%LOCALAPPDATA%\Omnikit\downloads</c>, damit ein zweiter Stick nicht neu lädt.
/// </summary>
public sealed class WindowsDownloader
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly ILogger<WindowsDownloader> _logger;

    public WindowsDownloader(ILogger<WindowsDownloader> logger)
    {
        _logger = logger;
    }

    public static string DownloadDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Omnikit", "downloads");

    /// <summary>Lädt den Katalog (products.cab) und packt ihn mit expand.exe aus (in Windows enthalten).</summary>
    public async Task<IReadOnlyList<WindowsDownload>> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.TempDirectory);
        var folder = Path.Combine(AppPaths.TempDirectory, "catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var cab = Path.Combine(folder, "products.cab");
            await using (var output = File.Create(cab))
            await using (var input = await Http.GetStreamAsync(WindowsCatalog.CatalogUri, cancellationToken))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            // Eigener Zielordner: expand verweigert sonst („Cannot expand a file onto itself“) und
            // benennt die ausgepackte Datei je nach Version nach der CAB-Datei statt products.xml.
            var extracted = Path.Combine(folder, "out");
            Directory.CreateDirectory(extracted);
            var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "expand.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                ArgumentList = { cab, "-F:products.xml", extracted },
            };
            using (var process = Process.Start(startInfo) ?? throw new IOException("expand.exe konnte nicht gestartet werden."))
            {
                await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
            }

            var xmlFile = Directory.GetFiles(extracted).SingleOrDefault() ?? throw new IOException("Der Windows-Katalog konnte nicht ausgepackt werden.");
            var catalog = WindowsCatalog.Parse(await File.ReadAllTextAsync(xmlFile, cancellationToken));
            _logger.LogInformation("Windows-Katalog geladen: {Count} Sprachen", catalog.Count);
            return catalog;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Lädt die Datei (setzt einen abgebrochenen Download fort) und prüft sie. Gibt den Pfad zurück.</summary>
    /// <exception cref="InvalidDataException">Die Prüfsumme passt nicht; die Datei wurde gelöscht.</exception>
    public async Task<string> DownloadAsync(WindowsDownload download, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DownloadDirectory);
        var path = Path.Combine(DownloadDirectory, Path.GetFileName(download.FileName));
        var partial = path + ".part";

        if (File.Exists(path) && new FileInfo(path).Length == download.SizeBytes && await VerifyAsync(path, download, progress, cancellationToken))
        {
            return path; // schon da und unverändert
        }

        File.Delete(path);

        // Bricht die Verbindung ab oder schläft ein, wird der Download bis zu fünfmal fortgesetzt.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadPartAsync(download, partial, progress, cancellationToken);
                break;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested
                && ex is HttpRequestException or IOException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "Download unterbrochen (Versuch {Attempt}), wird fortgesetzt", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken);
            }
        }

        File.Move(partial, path, overwrite: true);
        if (!await VerifyAsync(path, download, progress, cancellationToken))
        {
            File.Delete(path);
            _logger.LogError("Prüfsumme von {File} stimmt nicht, Datei gelöscht", download.FileName);
            throw new InvalidDataException("Die heruntergeladene Datei ist beschädigt oder verändert.");
        }

        _logger.LogInformation("Windows geladen und geprüft: {File}", download.FileName);
        return path;
    }

    private static async Task<bool> VerifyAsync(string path, WindowsDownload download, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        progress.Report(new DownloadProgress(download.SizeBytes, download.SizeBytes, IsVerifying: true));
        return string.Equals(await Sha1Async(path, cancellationToken), download.Sha1, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Lädt den Rest der Datei ab der Größe von <paramref name="partial"/>. Kommen 60 Sekunden lang keine Daten, wird abgebrochen.</summary>
    private static async Task DownloadPartAsync(WindowsDownload download, string partial, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var done = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (done >= download.SizeBytes)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, download.Uri);
        if (done > 0)
        {
            request.Headers.Range = new RangeHeaderValue(done, null); // fortsetzen
        }

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (done > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            done = 0; // Server setzt nicht fort: von vorn
        }

        await using var output = new FileStream(partial, done > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[1 << 20];
        var lastPercent = -1;
        while (true)
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stall.CancelAfter(StallTimeout);
            var read = await input.ReadAsync(buffer, stall.Token);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            done += read;
            var report = new DownloadProgress(done, download.SizeBytes);
            if (report.Percent != lastPercent)
            {
                lastPercent = report.Percent;
                progress.Report(report);
            }
        }
    }

    /// <summary>SHA-1 der Datei (so steht es im Microsoft-Katalog).</summary>
    public static async Task<string> Sha1Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
#pragma warning disable CA5350 // SHA-1 ist schwach gegen Kollisionen, aber Microsoft liefert nur diese Prüfsumme. Eine gefälschte Datei mit derselben SHA-1 zu einer vorgegebenen Datei ist praktisch nicht machbar.
        var hash = await SHA1.HashDataAsync(stream, cancellationToken);
#pragma warning restore CA5350
        return Convert.ToHexStringLower(hash);
    }
}
