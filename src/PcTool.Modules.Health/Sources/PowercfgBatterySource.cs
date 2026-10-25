using System.Diagnostics;
using System.Xml;
using Microsoft.Extensions.Logging;
using PcTool.Core;
using PcTool.Modules.Health.Data;

namespace PcTool.Modules.Health.Sources;

/// <summary>
/// Liest Akkudaten über "powercfg /batteryreport /xml" (in Windows eingebaut, keine Adminrechte nötig).
/// Der Bericht wird kurz im eigenen temp-Ordner gespeichert und danach sofort gelöscht.
/// </summary>
public sealed class PowercfgBatterySource : IBatterySource
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<PowercfgBatterySource> _logger;

    public PowercfgBatterySource(ILogger<PowercfgBatterySource> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<BatteryInfo>?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        Directory.CreateDirectory(AppPaths.TempDirectory);
        var reportPath = Path.Combine(AppPaths.TempDirectory, $"battery-{Guid.NewGuid():N}.xml");

        try
        {
            if (!await RunPowercfgAsync(reportPath, cancellationToken) || !File.Exists(reportPath))
            {
                return null;
            }

            var xml = await File.ReadAllTextAsync(reportPath, cancellationToken);
            return BatteryReportParser.Parse(xml);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Akku-Bericht konnte nicht gelesen werden");
            return null;
        }
        finally
        {
            File.Delete(reportPath);
        }
    }

    private async Task<bool> RunPowercfgAsync(string reportPath, CancellationToken cancellationToken)
    {
        // Voller Pfad zu System32, damit keine gleichnamige fremde powercfg.exe gestartet wird.
        // Argumente als Liste (Regel 6.1.4), nie als zusammengesetzter Text.
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powercfg.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "/batteryreport", "/xml", "/output", reportPath },
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        // Ausgaben mitlesen, sonst kann der Prozess bei vollem Puffer hängen bleiben.
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errors = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("powercfg beendet mit Code {ExitCode}: {Output} {Errors}", process.ExitCode, await output, await errors);
            return false;
        }

        return true;
    }
}
