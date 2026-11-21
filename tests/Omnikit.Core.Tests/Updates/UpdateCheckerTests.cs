using System.Net;
using System.Security.Cryptography;
using System.Text;
using Omnikit.Core.Elevation;
using Omnikit.Core.Updates;
using Omnikit.Operations;

namespace Omnikit.Core.Tests.Updates;

public sealed class UpdateCheckerTests : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _folder = Directory.CreateTempSubdirectory("omnikit-update-").FullName;
    private readonly byte[] _setup = Encoding.UTF8.GetBytes("ein kleines setup");

    public void Dispose()
    {
        _key.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Spielt die Release-Seite nach: Adresse → Inhalt; alles andere 404.</summary>
    private sealed class FakeReleases(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(files.TryGetValue(request.RequestUri!.ToString(), out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private byte[] Manifest(string version = "4.0.1", byte[]? setup = null)
    {
        var data = setup ?? _setup;
        return Encoding.UTF8.GetBytes($$"""{"format":1,"product":"app","version":"{{version}}","file":"Omnikit-{{version}}-setup.exe","size":{{data.Length}},"sha256":"{{Convert.ToHexStringLower(SHA256.HashData(data))}}"}""");
    }

    private byte[] Sign(byte[] data) => _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    private (UpdateChecker Checker, FakeReleases Releases) Checker(byte[] manifest, byte[]? signature = null, byte[]? setup = null)
    {
        var releases = new FakeReleases(new()
        {
            [UpdateSource.LatestManifest(UpdateProduct.App).ToString()] = manifest,
            [UpdateSource.LatestSignature(UpdateProduct.App).ToString()] = signature ?? Sign(manifest),
            [UpdateSource.Setup(UpdateProduct.App, new Version(4, 0, 1)).ToString()] = setup ?? _setup,
        });
        return (new UpdateChecker(new HttpClient(releases), [_key.ExportSubjectPublicKeyInfo()]), releases);
    }

    [Fact]
    public async Task GenuineUpdateIsFoundDownloadedAndCheckedOnlyFromTheFixedAddresses()
    {
        var (checker, releases) = Checker(Manifest());

        var offer = await checker.FindAsync(UpdateProduct.App, new Version(4, 0, 0), Ct);
        var files = await checker.DownloadAsync(UpdateProduct.App, offer!, _folder, null, Ct);

        Assert.Equal(new Version(4, 0, 1), offer!.Version);
        Assert.Equal(_setup, await File.ReadAllBytesAsync(files.Setup, Ct));
        Assert.Equal(64, new FileInfo(files.Signature).Length);
        Assert.All(releases.Requests, uri => Assert.StartsWith(UpdateSource.Releases + "/", uri.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForgedOrMissingAnswersGiveNoUpdate()
    {
        var forged = Manifest();
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.Null(await Checker(forged, stranger.SignData(forged, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)).Checker.FindAsync(UpdateProduct.App, new Version(4, 0, 0), Ct));
        Assert.Null(await Checker(Manifest()).Checker.FindAsync(UpdateProduct.App, new Version(4, 0, 1), Ct));      // schon aktuell
        Assert.Null(await Checker(new byte[UpdateVerifier.MaxManifestBytes + 10]).Checker.FindAsync(UpdateProduct.App, new Version(4, 0, 0), Ct));
        Assert.Null(await new UpdateChecker(new HttpClient(new FakeReleases([])), [_key.ExportSubjectPublicKeyInfo()]).FindAsync(UpdateProduct.App, new Version(4, 0, 0), Ct));
    }

    [Fact]
    public async Task ReplacedOrOversizedSetupIsRefused()
    {
        var (replaced, _) = Checker(Manifest(), setup: Encoding.UTF8.GetBytes("ein kleines setuq"));
        var offer = await replaced.FindAsync(UpdateProduct.App, new Version(4, 0, 0), Ct);
        await Assert.ThrowsAsync<InvalidDataException>(() => replaced.DownloadAsync(UpdateProduct.App, offer!, _folder, null, Ct));
        Assert.False(File.Exists(Path.Combine(_folder, "Omnikit-4.0.1-setup.exe")));   // nur die .part-Datei, nie unter dem echten Namen

        var (oversized, _) = Checker(Manifest(), setup: [.. _setup, .. new byte[100_000]]);
        offer = await oversized.FindAsync(UpdateProduct.App, new Version(4, 0, 0), Ct);
        await Assert.ThrowsAsync<InvalidDataException>(() => oversized.DownloadAsync(UpdateProduct.App, offer!, _folder, null, Ct));
    }

    [Fact]
    public void HelperOnlyAcceptsThreeExistingLocalFilesOfTheRightSize()
    {
        var manifest = Path.Combine(_folder, "m.json");
        var signature = Path.Combine(_folder, "m.json.sig");
        var setup = Path.Combine(_folder, "setup.exe");
        File.WriteAllBytes(manifest, Manifest());
        File.WriteAllBytes(signature, new byte[64]);
        File.WriteAllBytes(setup, _setup);

        Assert.Equal((manifest, signature, setup), InstallUpdateOperation.ParseArguments([manifest, signature, setup]));
        Assert.Throws<ElevatedOperationException>(() => InstallUpdateOperation.ParseArguments([manifest, signature]));
        Assert.Throws<ElevatedOperationException>(() => InstallUpdateOperation.ParseArguments(["m.json", signature, setup]));       // relativ
        Assert.Throws<ElevatedOperationException>(() => InstallUpdateOperation.ParseArguments([manifest, setup, setup]));           // Signatur nicht 64 Bytes
        Assert.Throws<ElevatedOperationException>(() => InstallUpdateOperation.ParseArguments([manifest, signature, setup + "x"])); // fehlt
        File.WriteAllBytes(manifest, new byte[UpdateVerifier.MaxManifestBytes + 1]);
        Assert.Throws<ElevatedOperationException>(() => InstallUpdateOperation.ParseArguments([manifest, signature, setup]));
    }

    [Fact]
    public void SetupRunsSilentlyWithoutRestartAndWritesALog()
    {
        var arguments = UpdateSource.SilentSetupArguments(@"C:\Program Files\Omnikit Updates\setup.log");

        Assert.Contains("/VERYSILENT", arguments);
        Assert.Contains("/NORESTART", arguments);
        Assert.Contains(@"/LOG=C:\Program Files\Omnikit Updates\setup.log", arguments);
    }
}
