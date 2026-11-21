using System.Security.Cryptography;
using System.Text;
using Omnikit.Core.Updates;

namespace Omnikit.Core.Tests.Updates;

public sealed class UpdateVerifierTests : IDisposable
{
    private static readonly Version Installed = new(4, 0, 0);
    private static readonly string Hash = new('a', 64);

    // Jeder Test erzeugt eigene Schlüssel; im Produkt steckt kein Testschlüssel
    private readonly ECDsa _release = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public void Dispose()
    {
        _release.Dispose();
        _stranger.Dispose();
    }

    private byte[][] Trusted => [_release.ExportSubjectPublicKeyInfo()];

    private static byte[] Manifest(string product = "app", string version = "4.0.1", string? file = null, long size = 1000, string? sha = null, int format = 1) =>
        Encoding.UTF8.GetBytes($$"""{"format":{{format}},"product":"{{product}}","version":"{{version}}","file":"{{file ?? (product == "app" ? $"Omnikit-{version}-setup.exe" : $"OmnikitAgent-{version}-setup.exe")}}","size":{{size}},"sha256":"{{sha ?? Hash}}"}""");

    private static byte[] Sign(ECDsa key, byte[] data) => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    private UpdateVerdict Check(byte[] manifest, byte[]? signature = null, UpdateProduct product = UpdateProduct.App) =>
        UpdateVerifier.Verify(manifest, signature ?? Sign(_release, manifest), Trusted, product, Installed, out _);

    [Fact]
    public void GenuineNewerUpdateIsAccepted()
    {
        var manifest = Manifest();

        Assert.Equal(UpdateVerdict.Valid, UpdateVerifier.Verify(manifest, Sign(_release, manifest), Trusted, UpdateProduct.App, Installed, out var parsed));
        Assert.Equal("4.0.1", parsed!.Version);
    }

    [Fact]
    public void ForeignOrBrokenSignaturesAreRejectedBeforeReading()
    {
        var manifest = Manifest();
        var signature = Sign(_release, manifest);
        var tampered = (byte[])manifest.Clone();
        tampered[^3] ^= 1;

        Assert.Equal(UpdateVerdict.BadSignature, Check(manifest, Sign(_stranger, manifest)));     // fremder Schlüssel
        Assert.Equal(UpdateVerdict.BadSignature, Check(tampered, signature));                      // ein Bit geändert
        Assert.Equal(UpdateVerdict.BadSignature, Check(manifest, signature[..63]));                // zu kurz
        Assert.Equal(UpdateVerdict.BadSignature, Check(manifest, []));
        Assert.Equal(UpdateVerdict.BadSignature, UpdateVerifier.Verify(manifest, signature, [], UpdateProduct.App, Installed, out _)); // keine Schlüssel
    }

    [Fact]
    public void TooLargeManifestsAreNotEvenChecked()
    {
        var huge = new byte[UpdateVerifier.MaxManifestBytes + 1];

        Assert.Equal(UpdateVerdict.BadSignature, Check(huge));
    }

    [Fact]
    public void SignedAgentSetupIsNoAppUpdate()
    {
        Assert.Equal(UpdateVerdict.WrongProduct, Check(Manifest(product: "agent")));
        Assert.Equal(UpdateVerdict.Valid, Check(Manifest(product: "agent"), product: UpdateProduct.Agent));
    }

    [Theory]
    [InlineData("4.0.0")]   // gleich
    [InlineData("3.9.9")]   // älter: kein Zurückstufen
    public void OnlyNewerVersionsCount(string version) => Assert.Equal(UpdateVerdict.NotNewer, Check(Manifest(version: version)));

    [Fact]
    public void SignedButInvalidContentIsRejected()
    {
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(version: "4.1.0-rc.1", file: "Omnikit-4.1.0-setup.exe")));   // Vorabversion
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(file: @"..\..\Windows\evil.exe")));                       // fremder Dateiname
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(file: "Omnikit-4.0.2-setup.exe")));                       // passt nicht zur Version
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(size: 0)));
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(size: UpdateVerifier.MaxSetupBytes + 1)));
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(sha: "xyz")));
        Assert.Equal(UpdateVerdict.Malformed, Check(Manifest(format: 2)));
        Assert.Equal(UpdateVerdict.Malformed, Check(Encoding.UTF8.GetBytes("kein json")));
    }

    [Fact]
    public async Task DownloadedSetupMustMatchSizeAndHash()
    {
        var setup = Encoding.UTF8.GetBytes("setup");
        var hash = Convert.ToHexStringLower(SHA256.HashData(setup));
        var manifest = new UpdateManifest(1, "app", "4.0.1", "Omnikit-4.0.1-setup.exe", setup.Length, hash);
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await UpdateVerifier.MatchesAsync(new MemoryStream(setup), manifest, ct));
        Assert.False(await UpdateVerifier.MatchesAsync(new MemoryStream(Encoding.UTF8.GetBytes("setuq")), manifest, ct));   // ausgetauscht
        Assert.False(await UpdateVerifier.MatchesAsync(new MemoryStream(Encoding.UTF8.GetBytes("setup!")), manifest, ct));  // andere Größe
    }

    [Fact]
    public void AddressesComeFromTheFixedBaseNotFromTheManifest()
    {
        var setup = UpdateSource.Setup(UpdateProduct.Agent, new Version(4, 2, 0));

        Assert.Equal("https", setup.Scheme);
        Assert.Equal("https://github.com/alexander-eppacher/Omnikit/releases/download/v4.2.0/OmnikitAgent-4.2.0-setup.exe", setup.ToString());
        Assert.EndsWith("/latest/download/omnikit-app-update.json", UpdateSource.LatestManifest(UpdateProduct.App).ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("4.0.0-dev+abc123", "4.0.0")]
    [InlineData("4.1.2", "4.1.2")]
    [InlineData(null, "0.0.0")]
    public void InstalledVersionIgnoresSuffixes(string? informational, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateVerifier.InstalledVersion(informational));

    [Fact]
    public void NoTrustedKeyIsATestKey()
    {
        // Vor dem ersten Release ist die Liste leer (dann gilt kein Update). Danach darf nie ein hier erzeugter Schlüssel darin stehen.
        Assert.DoesNotContain(UpdateSource.TrustedKeys, key => key.AsSpan().SequenceEqual(_release.ExportSubjectPublicKeyInfo()));
        Assert.All(UpdateSource.TrustedKeys, key =>
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(key, out _);
            Assert.Equal(256, ecdsa.KeySize);
        });
    }
}
