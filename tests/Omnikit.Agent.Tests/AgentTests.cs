using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Modules.Apps.Winget;
using Omnikit.Shared;

namespace Omnikit.Agent.Tests;

public class AgentCommandTests
{
    [Fact]
    public void RegisterNeedsHttpsServerAndCode()
    {
        var command = AgentCommand.Parse(["register", "--server", "https://omnikit.local/", "--code", "ABCDE-FGHJK"]);

        var register = Assert.IsType<AgentCommand.Register>(command);
        Assert.Equal("omnikit.local", register.Server.Host);
        Assert.Equal("ABCDE-FGHJK", register.Code);
        Assert.Null(register.CaFile);
    }

    [Theory]
    [InlineData("register", "--server", "http://omnikit.local/", "--code", "X")]
    [InlineData("register", "--server", "ftp://omnikit.local/", "--code", "X")]
    [InlineData("register", "--code", "X")]
    [InlineData("register", "--server", "https://omnikit.local/", "--port", "1")]
    [InlineData("format", "c:")]
    public void BadInputShowsHelpWithAReason(params string[] args)
    {
        var help = Assert.IsType<AgentCommand.Help>(AgentCommand.Parse(args));

        Assert.NotNull(help.Error);
    }

    [Fact]
    public void OtherCommands()
    {
        Assert.IsType<AgentCommand.Install>(AgentCommand.Parse(["install"]));
        Assert.IsType<AgentCommand.Uninstall>(AgentCommand.Parse(["UNINSTALL"]));
        Assert.IsType<AgentCommand.Run>(AgentCommand.Parse(["run"]));
        Assert.IsType<AgentCommand.Once>(AgentCommand.Parse(["once"]));
        Assert.Null(Assert.IsType<AgentCommand.Help>(AgentCommand.Parse([])).Error);
    }

    [Fact]
    public void HttpServerIsRefusedByTheClientToo()
    {
        Assert.Throws<ArgumentException>(() => new ServerClient(new Uri("http://omnikit.local/"), null, null));
    }
}

public class TaskRunnerTests
{
    private sealed class FakeWinget : IWingetClient
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InstalledPackage>>([]);

        public Task<IReadOnlyList<AvailableUpgrade>> GetUpgradesAsync(IReadOnlyList<InstalledPackage> installed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AvailableUpgrade>>([]);

        public Task<PackageResult> InstallAsync(string packageId, CancellationToken cancellationToken)
        {
            Calls.Add("install " + packageId);
            return Task.FromResult(new PackageResult(packageId, WingetOutcome.Success, 0));
        }

        public Task<PackageResult> UpgradeAsync(string packageId, CancellationToken cancellationToken)
        {
            Calls.Add("upgrade " + packageId);
            return Task.FromResult(new PackageResult(packageId, WingetOutcome.NoNetwork, unchecked((int)0x8A15000F)));
        }
    }

    [Fact]
    public async Task InstallAndUpgradeGoToWinget()
    {
        var winget = new FakeWinget();
        var runner = new TaskRunner(winget, NullLogger<TaskRunner>.Instance);

        var installed = await runner.RunAsync(new AgentTask(1, AgentTaskKind.Install, "7zip.7zip"), TestContext.Current.CancellationToken);
        var upgraded = await runner.RunAsync(new AgentTask(2, AgentTaskKind.Upgrade, "VideoLAN.VLC"), TestContext.Current.CancellationToken);

        Assert.True(installed.Success);
        Assert.False(upgraded.Success);
        Assert.Equal(["install 7zip.7zip", "upgrade VideoLAN.VLC"], winget.Calls);
    }

    [Theory]
    [InlineData("--source evil")]
    [InlineData("7zip.7zip & calc")]
    [InlineData("")]
    public async Task InvalidPackageIdsNeverReachWinget(string packageId)
    {
        var winget = new FakeWinget();

        var result = await new TaskRunner(winget, NullLogger<TaskRunner>.Instance)
            .RunAsync(new AgentTask(1, AgentTaskKind.Install, packageId), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Empty(winget.Calls);
    }

    [Fact]
    public async Task UnknownTaskKindIsNotExecuted()
    {
        var winget = new FakeWinget();

        var result = await new TaskRunner(winget, NullLogger<TaskRunner>.Instance)
            .RunAsync(new AgentTask(1, (AgentTaskKind)99, "7zip.7zip"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Empty(winget.Calls);
    }
}

public class WingetLocatorTests
{
    [Fact]
    public void NewestAppInstallerFolderWins()
    {
        string[] folders =
        [
            @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.21.3482.0_x64__8wekyb3d8bbwe",
            @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.26.430.0_x64__8wekyb3d8bbwe",
            @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.9.25200.0_x64__8wekyb3d8bbwe",
            @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_kaputt",
        ];

        Assert.EndsWith("1.26.430.0_x64__8wekyb3d8bbwe", WingetLocator.Newest(folders), StringComparison.Ordinal);
    }
}

public class CertificateTests
{
    /// <summary>Zertifizierungsstelle mit privatem Schlüssel (zum Ausstellen); ohne <paramref name="issuer"/> selbst ausgestellt.</summary>
    private static X509Certificate2 CreateCa(string name, X509Certificate2? issuer = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }

        using var issued = request.Create(issuer, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMonths(6), [5, 6, 7, 8]);
        return issued.CopyWithPrivateKey(key);
    }

    /// <summary>Server-Zertifikat wie von Caddy: kurz gültig, Zweck „TLS-Server“.</summary>
    private static X509Certificate2 Issue(X509Certificate2 issuer, string eku = "1.3.6.1.5.5.7.3.1", int validHours = 12)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=omnikit.local", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(eku)], false));
        // Negative Stunden: schon abgelaufen (seit 8 Stunden), aber trotzdem innerhalb der Gültigkeit der Ausstellerin
        var start = validHours > 0 ? DateTimeOffset.UtcNow.AddHours(-1) : DateTimeOffset.UtcNow.AddHours(validHours - 8);
        return request.Create(issuer, start, start.AddHours(Math.Abs(validHours)), [1, 2, 3, 4]);
    }

    /// <summary>Wie beim Agent bzw. vom Server: nur der öffentliche Teil, ohne Schlüssel.</summary>
    private static X509Certificate2 PublicOnly(X509Certificate2 certificate) => X509CertificateLoader.LoadCertificate(certificate.RawData);

    private static readonly SslPolicyErrors ChainErrors = SslPolicyErrors.RemoteCertificateChainErrors;

    [Fact]
    public void CaddyChainWithIntermediateIsAccepted()
    {
        using var root = CreateCa("Caddy Local Authority - 2026 ECC Root");
        using var intermediate = CreateCa("Caddy Local Authority - ECC Intermediate", root);
        using var server = Issue(intermediate);

        Assert.True(ServerClient.IsTrusted(server, [PublicOnly(server), PublicOnly(intermediate)], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void CertificateDirectlyFromTheRootIsAccepted()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var server = Issue(root);

        Assert.True(ServerClient.IsTrusted(server, [], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void WithoutTheIntermediateThereIsNoPath()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var intermediate = CreateCa("Zwischen", root);
        using var server = Issue(intermediate);

        Assert.False(ServerClient.IsTrusted(server, [], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void CertificateFromAnotherRootIsRefused()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var other = CreateCa("Fremde Stelle");
        using var server = Issue(other);

        Assert.False(ServerClient.IsTrusted(server, [], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void ForeignIntermediateWithTheSameNameIsRefused()
    {
        // Ein Angreifer baut eine eigene Kette mit denselben Namen wie Caddy
        using var root = CreateCa("Caddy Local Authority - 2026 ECC Root");
        using var fakeRoot = CreateCa("Caddy Local Authority - 2026 ECC Root");
        using var fakeIntermediate = CreateCa("Caddy Local Authority - ECC Intermediate", fakeRoot);
        using var server = Issue(fakeIntermediate);

        Assert.False(ServerClient.IsTrusted(server, [PublicOnly(server), PublicOnly(fakeIntermediate), PublicOnly(fakeRoot)], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void CertificateWindowsTrustsIsStillRefusedWhenARootIsPinned()
    {
        // Früher galt „Windows vertraut schon → in Ordnung“. Mit eigener CA gilt nur noch diese.
        using var root = CreateCa("Omnikit Test Root");
        using var publicCa = CreateCa("Öffentliche oder Firmen-Stelle");
        using var server = Issue(publicCa);

        Assert.False(ServerClient.IsTrusted(server, [PublicOnly(server)], SslPolicyErrors.None, PublicOnly(root)));
    }

    [Fact]
    public void ExpiredCertificateIsRefused()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var server = Issue(root, validHours: -12);

        Assert.False(ServerClient.IsTrusted(server, [], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void ClientOnlyCertificateIsRefused()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var client = Issue(root, eku: "1.3.6.1.5.5.7.3.2");

        Assert.False(ServerClient.IsTrusted(client, [], ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void WrongNameIsAlwaysRefused()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var server = Issue(root);

        Assert.False(ServerClient.IsTrusted(server, [], SslPolicyErrors.RemoteCertificateNameMismatch | ChainErrors, PublicOnly(root)));
    }

    [Fact]
    public void OnlyARootCertificateFitsCa()
    {
        using var root = CreateCa("Omnikit Test Root");
        using var intermediate = CreateCa("Zwischen", root);
        using var server = Issue(intermediate);

        Assert.True(ServerClient.IsRootCertificate(PublicOnly(root)));
        Assert.False(ServerClient.IsRootCertificate(PublicOnly(intermediate)));
        Assert.False(ServerClient.IsRootCertificate(PublicOnly(server)));
    }
}

public class TaskLoopTests
{
    /// <summary>Server im Speicher: gibt Aufgaben einzeln heraus und kann beim Melden absichtlich ausfallen.</summary>
    private sealed class FakeServer(params AgentTask[] tasks) : IAgentServer
    {
        private readonly Queue<AgentTask> _open = new(tasks);

        public int FailuresBeforeSuccess { get; set; }

        public List<int> Results { get; } = [];

        public int ResultCalls { get; private set; }

        public Task SendReportAsync(ReportRequest report, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<AgentTask>> GetTasksAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentTask>>(_open.TryDequeue(out var task) ? [task] : []);

        public Task<bool> SendResultAsync(int taskId, TaskResultRequest result, CancellationToken cancellationToken)
        {
            ResultCalls++;
            if (FailuresBeforeSuccess-- > 0)
            {
                throw new HttpRequestException("Netz weg");
            }

            Results.Add(taskId);
            return Task.FromResult(true);
        }
    }

    private sealed class CountingWinget : IWingetClient
    {
        public int Installs { get; private set; }

        public Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InstalledPackage>>([]);

        public Task<IReadOnlyList<AvailableUpgrade>> GetUpgradesAsync(IReadOnlyList<InstalledPackage> installed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AvailableUpgrade>>([]);

        public Task<PackageResult> InstallAsync(string packageId, CancellationToken cancellationToken)
        {
            Installs++;
            return Task.FromResult(new PackageResult(packageId, WingetOutcome.Success, 0));
        }

        public Task<PackageResult> UpgradeAsync(string packageId, CancellationToken cancellationToken) => InstallAsync(packageId, cancellationToken);
    }

    private static TaskLoop Loop(IAgentServer server, IWingetClient winget) =>
        new(server, new TaskRunner(winget, NullLogger<TaskRunner>.Instance), NullLogger<TaskLoop>.Instance, TimeSpan.Zero);

    [Fact]
    public async Task TasksAreFetchedOneAfterAnother()
    {
        var server = new FakeServer(new AgentTask(1, AgentTaskKind.Install, "7zip.7zip"), new AgentTask(2, AgentTaskKind.Install, "VideoLAN.VLC"));

        var done = await Loop(server, new CountingWinget()).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, done);
        Assert.Equal([1, 2], server.Results);
    }

    [Fact]
    public async Task ResultIsSentAgainAfterANetworkErrorButNotInstalledAgain()
    {
        var server = new FakeServer(new AgentTask(1, AgentTaskKind.Install, "7zip.7zip")) { FailuresBeforeSuccess = 2 };
        var winget = new CountingWinget();

        await Loop(server, winget).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1], server.Results);
        Assert.Equal(3, server.ResultCalls);
        Assert.Equal(1, winget.Installs);
    }

    [Fact]
    public async Task AfterThreeFailedAttemptsTheCycleStops()
    {
        var server = new FakeServer(new AgentTask(1, AgentTaskKind.Install, "7zip.7zip"), new AgentTask(2, AgentTaskKind.Install, "VideoLAN.VLC"))
        {
            FailuresBeforeSuccess = TaskLoop.ResultAttempts,
        };
        var winget = new CountingWinget();

        await Assert.ThrowsAsync<HttpRequestException>(() => Loop(server, winget).RunAsync(TestContext.Current.CancellationToken));

        // Die zweite Aufgabe bleibt beim Server freigegeben und kommt beim nächsten Durchgang
        Assert.Equal(1, winget.Installs);
        Assert.Equal(TaskLoop.ResultAttempts, server.ResultCalls);
    }
}

public sealed class AgentStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "omnikit-agent-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void SavedSettingsCanBeLoaded()
    {
        var store = new AgentStore(_folder);

        store.Save(new AgentConfig(new Uri("https://omnikit.local/"), 7, false), "geheim", null);

        Assert.Equal(new AgentConfig(new Uri("https://omnikit.local/"), 7, false), store.LoadConfig());
        Assert.Equal("geheim", store.LoadToken());
        Assert.Empty(Directory.GetFiles(_folder, "*.neu"));
    }

    [Fact]
    public void BrokenSettingsAreNotTheSameAsNotRegistered()
    {
        var store = new AgentStore(_folder);
        Assert.Null(store.LoadConfig());

        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "agent.json"), "{ kaputt");
        Assert.Throws<InvalidDataException>(store.LoadConfig);

        File.WriteAllText(Path.Combine(_folder, "agent.json"), "{}");
        Assert.Throws<InvalidDataException>(store.LoadConfig);
    }

    [Fact]
    public void CaFileMayBeTheAlreadyStoredOne()
    {
        // „register … --ca“ mit genau der Datei, die der Agent schon gespeichert hat
        var store = new AgentStore(_folder);
        Directory.CreateDirectory(_folder);
        File.WriteAllText(store.CaFile, "zertifikat");

        store.Save(new AgentConfig(new Uri("https://omnikit.local/"), 7, true), "geheim", store.CaFile);

        Assert.Equal("zertifikat", File.ReadAllText(store.CaFile));
    }
}
