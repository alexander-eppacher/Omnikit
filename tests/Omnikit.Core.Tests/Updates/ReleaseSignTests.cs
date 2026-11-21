using System.Security.Cryptography;
using Omnikit.Core.Updates;

namespace Omnikit.Core.Tests.Updates;

/// <summary>Der ganze Weg des Entwicklers: Schlüssel erzeugen, Manifest schreiben, signieren – und Omnikit erkennt es als echt.</summary>
public sealed class ReleaseSignTests : IDisposable
{
    private const string Password = "ein-langes-test-passwort";
    private readonly string _folder = Directory.CreateTempSubdirectory("omnikit-sign-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string FileIn(string name) => Path.Combine(_folder, name);

    [Fact]
    public void SignedReleaseIsRecognizedAsGenuine()
    {
        var key = FileIn("schluessel.pem");
        var setup = FileIn("Omnikit-4.0.1-setup.exe");
        var manifest = FileIn("omnikit-app-update.json");
        File.WriteAllBytes(setup, [1, 2, 3, 4]);

        Assert.Equal(0, ReleaseSigning.KeyGen(key, Password));
        Assert.Contains("ENCRYPTED PRIVATE KEY", File.ReadAllText(key), StringComparison.Ordinal);   // nie unverschlüsselt
        Assert.DoesNotContain("PRIVATE", File.ReadAllText(key + ".pub"), StringComparison.Ordinal);  // daneben nur der öffentliche Teil
        Assert.Equal(0, ReleaseSigning.WriteManifest("app", "4.0.1", setup, manifest));
        Assert.Equal(0, ReleaseSigning.Sign(key, manifest, Password));

        // Geprüft wird mit dem Inhalt der .pub-Datei – genau das, was das Skript in UpdateSource.TrustedKeys einträgt
        var verdict = UpdateVerifier.Verify(File.ReadAllBytes(manifest), File.ReadAllBytes(manifest + ".sig"),
            [Convert.FromBase64String(File.ReadAllText(key + ".pub"))], UpdateProduct.App, new Version(4, 0, 0), out var parsed);
        Assert.Equal(UpdateVerdict.Valid, verdict);
        Assert.Equal(4, parsed!.Size);
    }

    [Fact]
    public void ExistingKeysAreNeverOverwrittenAndShortPasswordsAreRefused()
    {
        var key = FileIn("schluessel.pem");
        File.WriteAllText(key, "vorhanden");

        Assert.Equal(1, ReleaseSigning.KeyGen(key, Password));
        Assert.Equal("vorhanden", File.ReadAllText(key));
        Assert.Equal(1, ReleaseSigning.KeyGen(FileIn("neu.pem"), "kurz"));
        Assert.False(File.Exists(FileIn("neu.pem")));
    }

    [Fact]
    public void WrongPasswordCannotSign()
    {
        var key = FileIn("schluessel.pem");
        var manifest = FileIn("m.json");
        File.WriteAllText(manifest, "{}");
        ReleaseSigning.KeyGen(key, Password);

        Assert.ThrowsAny<CryptographicException>(() => ReleaseSigning.Sign(key, manifest, "falsches-passwort-123"));
    }

    [Fact]
    public void SetupMustCarryTheExpectedName()
    {
        var setup = FileIn("setup.exe");
        File.WriteAllBytes(setup, [1]);

        Assert.Equal(1, ReleaseSigning.WriteManifest("app", "4.0.1", setup, FileIn("m.json")));
        Assert.Equal(1, ReleaseSigning.WriteManifest("server", "4.0.1", setup, FileIn("m.json")));
        Assert.Equal(1, ReleaseSigning.WriteManifest("app", "4.0.1-rc.1", setup, FileIn("m.json")));
    }
}
