namespace Omnikit.Core.Updates;

/// <summary>
/// Woher Updates kommen und wem sie vertrauen. Alles fest im Code: keine Adresse vom Server, aus Einstellungen oder vom Benutzer.
/// </summary>
public static class UpdateSource
{
    /// <summary>Die einzige Quelle: die Release-Seite des Projekts auf GitHub (nur HTTPS).</summary>
    public const string Releases = "https://github.com/alexander-eppacher/Omnikit/releases";

    /// <summary>Manifest des neuesten Releases (GitHub leitet auf die Datei des neuesten Releases weiter).</summary>
    public static Uri LatestManifest(UpdateProduct product) => new($"{Releases}/latest/download/{ManifestName(product)}");

    /// <summary>Signatur zum Manifest (64 Bytes, ECDSA P-256).</summary>
    public static Uri LatestSignature(UpdateProduct product) => new($"{Releases}/latest/download/{ManifestName(product)}.sig");

    /// <summary>Adresse des Setups, gebaut aus der festen Basis und der bereits geprüften Version – nie aus dem Manifest übernommen.</summary>
    public static Uri Setup(UpdateProduct product, Version version) =>
        new($"{Releases}/download/v{UpdateVerifier.Format(version)}/{UpdateVerifier.SetupFileName(product, version)}");

    /// <summary>Setup-Schalter von Inno Setup: still, ohne Rückfragen, ohne Neustart; offene Omnikit-Programme werden geschlossen.</summary>
    public static IReadOnlyList<string> SilentSetupArguments(string logFile) =>
        ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/NOCANCEL", "/CLOSEAPPLICATIONS", "/LOG=" + logFile];

    public static string ManifestName(UpdateProduct product) => $"omnikit-{UpdateVerifier.ProductName(product)}-update.json";

    /// <summary>
    /// Öffentliche Schlüssel (SubjectPublicKeyInfo), denen Updates vertrauen: der aktive und ein Reserveschlüssel für den Notfall.
    /// Die privaten Schlüssel erzeugt und verwahrt der Entwickler offline (<c>tools/ReleaseSign</c>, <c>docs/Release.md</c>).
    /// Solange die Liste leer ist, wird kein Update als echt erkannt.
    /// </summary>
    public static IReadOnlyList<byte[]> TrustedKeys { get; } =
    [
        // aktiv
        Convert.FromBase64String("MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbj43bLclXNcjGXezms6ZG6GliGyL+b5NH1CPZYoVHACvqnd4nRCiw3dT5s/7qhm+a0JrFJ/7O+1EnHZvlA+Ihw=="),
        // Reserve (nur für den Notfall, siehe docs/Release.md)
        Convert.FromBase64String("MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEywXmfyW0tkkQrngMgAaH0MSKFIvbYur/57rV1yZ3ribsBohD6mFOX51cywSRhBsv59YViZB4Qy4I6DxW7RV88w=="),
    ];
}
