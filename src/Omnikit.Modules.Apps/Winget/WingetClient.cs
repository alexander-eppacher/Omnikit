using Microsoft.Extensions.Logging;
using Omnikit.Core;

namespace Omnikit.Modules.Apps.Winget;

/// <summary>Was winget für die Seite „Apps“ erledigt.</summary>
public interface IWingetClient
{
    /// <summary>Installierte Programme, die winget kennt. Nur lesen.</summary>
    Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken);

    /// <summary>Programme mit neuerer Version. Nur lesen.</summary>
    /// <param name="installed">Zum Auflösen gekürzter IDs aus der Tabelle.</param>
    Task<IReadOnlyList<AvailableUpgrade>> GetUpgradesAsync(IReadOnlyList<InstalledPackage> installed, CancellationToken cancellationToken);

    /// <summary>Installiert ein Programm still. Nur nach Bestätigung durch den Nutzer aufrufen.</summary>
    Task<PackageResult> InstallAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>Aktualisiert ein Programm still. Nur nach Bestätigung durch den Nutzer aufrufen.</summary>
    Task<PackageResult> UpgradeAsync(string packageId, CancellationToken cancellationToken);
}

/// <summary>Ergebnis für ein einzelnes Programm.</summary>
public sealed record PackageResult(string PackageId, WingetOutcome Outcome, int ExitCode)
{
    public bool IsSuccess => WingetExitCodes.IsSuccess(Outcome);
}

public sealed class WingetClient : IWingetClient
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromMinutes(2);

    // Große Programme (z. B. LibreOffice) brauchen bei langsamem Netz lange; danach wird winget beendet.
    private static readonly TimeSpan PackageTimeout = TimeSpan.FromMinutes(30);

    // Bei jedem Aufruf: Nutzungsbedingungen der Quelle bestätigen und niemals nachfragen (sonst hinge der Aufruf).
    private static readonly string[] Unattended = ["--accept-source-agreements", "--disable-interactivity"];

    private readonly IWingetRunner _runner;
    private readonly ILogger<WingetClient> _logger;
    private readonly string _tempDirectory;

    public WingetClient(IWingetRunner runner, ILogger<WingetClient> logger)
        : this(runner, logger, AppPaths.TempDirectory)
    {
    }

    internal WingetClient(IWingetRunner runner, ILogger<WingetClient> logger, string tempDirectory)
    {
        _runner = runner;
        _logger = logger;
        _tempDirectory = tempDirectory;
    }

    public async Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_tempDirectory);
        var exportPath = Path.Combine(_tempDirectory, $"winget-export-{Guid.NewGuid():N}.json");
        try
        {
            var result = await _runner.RunAsync(["export", "--output", exportPath, "--include-versions", .. Unattended], QueryTimeout, cancellationToken);
            ThrowIfFailed("export", result);

            return File.Exists(exportPath) ? ExportParser.Parse(await File.ReadAllTextAsync(exportPath, cancellationToken)) : [];
        }
        finally
        {
            File.Delete(exportPath);
        }
    }

    public async Task<IReadOnlyList<AvailableUpgrade>> GetUpgradesAsync(IReadOnlyList<InstalledPackage> installed, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(["upgrade", .. Unattended], QueryTimeout, cancellationToken);
        if (WingetExitCodes.Classify(result.ExitCode) == WingetOutcome.NothingToDo)
        {
            return [];
        }

        ThrowIfFailed("upgrade", result);

        return UpgradeTableParser.Parse(result.Output)
            .Select(upgrade => upgrade with { Id = ResolveId(upgrade.Id, installed) ?? string.Empty })
            .Where(upgrade => PackageId.IsValid(upgrade.Id))
            .ToList();
    }

    public Task<PackageResult> InstallAsync(string packageId, CancellationToken cancellationToken) =>
        RunPackageAsync("install", packageId, ["--source", "winget"], cancellationToken);

    public Task<PackageResult> UpgradeAsync(string packageId, CancellationToken cancellationToken) =>
        RunPackageAsync("upgrade", packageId, [], cancellationToken);

    private async Task<PackageResult> RunPackageAsync(string command, string packageId, string[] extra, CancellationToken cancellationToken)
    {
        if (!PackageId.IsValid(packageId))
        {
            throw new ArgumentException("Ungültige Paket-ID.", nameof(packageId));
        }

        string[] arguments =
        [
            command, "--id", packageId, "--exact", .. extra,
            "--silent", "--accept-package-agreements", .. Unattended,
        ];

        _logger.LogInformation("winget {Command} {PackageId} gestartet", command, packageId);
        var result = await _runner.RunAsync(arguments, PackageTimeout, cancellationToken);
        var outcome = result.TimedOut ? WingetOutcome.TimedOut : WingetExitCodes.Classify(result.ExitCode);

        if (WingetExitCodes.IsSuccess(outcome))
        {
            _logger.LogInformation("winget {Command} {PackageId}: {Outcome}", command, packageId, outcome);
        }
        else
        {
            _logger.LogWarning(
                "winget {Command} {PackageId} fehlgeschlagen: {Outcome} ({ExitCode}). Letzte Ausgabe: {Output}",
                command, packageId, outcome, WingetExitCodes.Format(result.ExitCode), LastLines(result.Output));
        }

        return new PackageResult(packageId, outcome, result.ExitCode);
    }

    private void ThrowIfFailed(string command, WingetRunResult result)
    {
        if (result.ExitCode == 0 && !result.TimedOut)
        {
            return;
        }

        var outcome = result.TimedOut ? WingetOutcome.TimedOut : WingetExitCodes.Classify(result.ExitCode);
        _logger.LogWarning(
            "winget {Command} fehlgeschlagen: {Outcome} ({ExitCode}). Letzte Ausgabe: {Output}",
            command, outcome, WingetExitCodes.Format(result.ExitCode), LastLines(result.Output));
        throw new WingetException(outcome, result.ExitCode);
    }

    /// <summary>
    /// Ist die Spalte zu schmal, kürzt winget lange IDs mit „…“. Dann hilft die vollständige Liste der installierten Pakete,
    /// sofern genau ein Paket mit diesem Anfang installiert ist.
    /// </summary>
    internal static string? ResolveId(string id, IReadOnlyList<InstalledPackage> installed)
    {
        if (!id.EndsWith('…'))
        {
            return id;
        }

        var prefix = id.TrimEnd('…');
        var matches = installed.Where(package => package.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    /// <summary>Für das Log: nur das Ende der Ausgabe, dort steht der Fehlergrund.</summary>
    private static string LastLines(string output) =>
        string.Join(" | ", output.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).TakeLast(5));
}
