using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Modules.Apps.Winget;

namespace Omnikit.Modules.Apps.Tests.Winget;

public sealed class WingetClientTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "omnikit-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRunner _runner = new();

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private WingetClient CreateClient() => new(_runner, NullLogger<WingetClient>.Instance, _temp);

    [Fact]
    public async Task InstallUsesExactIdSilentModeAndOfficialSource()
    {
        var result = await CreateClient().InstallAsync("7zip.7zip", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["install", "--id", "7zip.7zip", "--exact", "--source", "winget", "--silent", "--accept-package-agreements",
             "--accept-source-agreements", "--disable-interactivity"],
            _runner.Calls.Single());
    }

    [Fact]
    public async Task InvalidIdIsNeverPassedToWinget()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateClient().UpgradeAsync("--all", CancellationToken.None));
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task FailureIsClassified()
    {
        _runner.ExitCode = unchecked((int)0x8A150101u);

        var result = await CreateClient().UpgradeAsync("VideoLAN.VLC", CancellationToken.None);

        Assert.Equal(WingetOutcome.AppInUse, result.Outcome);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TimeoutIsReported()
    {
        _runner.TimedOut = true;

        var result = await CreateClient().InstallAsync("VideoLAN.VLC", CancellationToken.None);

        Assert.Equal(WingetOutcome.TimedOut, result.Outcome);
    }

    [Fact]
    public async Task InstalledPackagesComeFromExportFileWhichIsDeletedAfterwards()
    {
        _runner.ExportJson = """{"Sources":[{"Packages":[{"PackageIdentifier":"7zip.7zip","Version":"26.03"}],"SourceDetails":{"Name":"winget"}}]}""";

        var installed = await CreateClient().GetInstalledAsync(CancellationToken.None);

        Assert.Equal([new InstalledPackage("7zip.7zip", "26.03", "winget")], installed);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [Fact]
    public async Task NoNetworkWhileListingThrowsWithOutcome()
    {
        _runner.ExitCode = unchecked((int)0x8A15004Bu);

        var error = await Assert.ThrowsAsync<WingetException>(() => CreateClient().GetInstalledAsync(CancellationToken.None));

        Assert.Equal(WingetOutcome.NoNetwork, error.Outcome);
    }

    [Fact]
    public async Task NoUpgradesGivesEmptyList()
    {
        _runner.ExitCode = unchecked((int)0x8A15002Bu);

        Assert.Empty(await CreateClient().GetUpgradesAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task ShortenedIdsAreResolvedOrDropped()
    {
        _runner.Output = string.Join('\n',
            "Name      Id                     Version Available Source",
            "---------------------------------------------------------",
            "VC++ x86  Microsoft.VCRedist.20… 14.40   14.51     winget",
            "Unknown   Some.Unknown.Packa…    1.0     2.0       winget");
        InstalledPackage[] installed = [new("Microsoft.VCRedist.2015+.x86", "14.40", "winget")];

        var upgrades = await CreateClient().GetUpgradesAsync(installed, CancellationToken.None);

        Assert.Equal(["Microsoft.VCRedist.2015+.x86"], upgrades.Select(u => u.Id));
    }

    [Fact]
    public async Task MissingWingetIsPassedOn()
    {
        _runner.Missing = true;

        await Assert.ThrowsAsync<WingetMissingException>(() => CreateClient().GetInstalledAsync(CancellationToken.None));
    }

    /// <summary>Spielt winget nach: merkt sich die Aufrufe und liefert vorgegebene Ergebnisse.</summary>
    private sealed class FakeRunner : IWingetRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public int ExitCode { get; set; }

        public string Output { get; set; } = string.Empty;

        public string? ExportJson { get; set; }

        public bool TimedOut { get; set; }

        public bool Missing { get; set; }

        public async Task<WingetRunResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (Missing)
            {
                throw new WingetMissingException();
            }

            Calls.Add(arguments);
            if (arguments[0] == "export" && ExportJson is not null)
            {
                await File.WriteAllTextAsync(arguments[2], ExportJson, cancellationToken);
            }

            return new WingetRunResult(ExitCode, Output, TimedOut);
        }
    }
}
