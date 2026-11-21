using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Omnikit.Core.Updates;

/// <summary>Wofür ein Update gilt. Ein gültig signiertes Agent-Setup darf nie als App-Update durchgehen und umgekehrt.</summary>
public enum UpdateProduct
{
    App,
    Agent,
}

/// <summary>Inhalt der Datei <c>omnikit-update.json</c> eines Releases (nach erfolgreicher Prüfung der Signatur).</summary>
/// <param name="Format">Aufbau der Datei; diese Version kennt nur 1.</param>
/// <param name="Product">„app“ oder „agent“.</param>
/// <param name="Version">Version des Setups, z. B. „4.0.1“ (ohne Vorab-Zusatz).</param>
/// <param name="File">Dateiname des Setups im Release, z. B. „Omnikit-4.0.1-setup.exe“.</param>
/// <param name="Size">Größe des Setups in Bytes.</param>
/// <param name="Sha256">SHA-256 des Setups (64 Hexadezimalzeichen).</param>
public sealed record UpdateManifest(int Format, string Product, string Version, string File, long Size, string Sha256);

public enum UpdateVerdict
{
    /// <summary>Echt, passendes Produkt und neuer als die installierte Version.</summary>
    Valid,

    /// <summary>Signatur fehlt, ist kaputt oder stammt von keinem eingebauten Schlüssel. Das Manifest wurde gar nicht erst gelesen.</summary>
    BadSignature,

    /// <summary>Echt signiert, aber Aufbau, Größe, Prüfsumme oder Dateiname sind ungültig.</summary>
    Malformed,

    /// <summary>Echt, aber für das andere Produkt (App statt Agent oder umgekehrt).</summary>
    WrongProduct,

    /// <summary>Echt, aber nicht neuer als die installierte Version (kein Zurückstufen auf alte, evtl. lückenhafte Versionen).</summary>
    NotNewer,
}

/// <summary>
/// Prüft Update-Manifeste. Vertrauen hängt nur an der eigenen Signatur (ECDSA P-256 mit SHA-256), deren privater Schlüssel offline
/// beim Entwickler liegt – nicht an GitHub, nicht an HTTPS allein. Reihenfolge: erst die Signatur über die rohen Bytes, dann lesen.
/// Ein Schalter zum Überspringen der Prüfung existiert bewusst nicht.
/// </summary>
public static partial class UpdateVerifier
{
    /// <summary>Ein Manifest ist wenige hundert Bytes groß; alles darüber wird gar nicht erst geprüft.</summary>
    public const int MaxManifestBytes = 4096;

    /// <summary>Kein Setup ist größer; schützt vor „Updates“, die die Platte füllen.</summary>
    public const long MaxSetupBytes = 1L << 30;

    public static UpdateVerdict Verify(
        ReadOnlySpan<byte> manifestBytes, ReadOnlySpan<byte> signature, IReadOnlyList<byte[]> trustedKeys,
        UpdateProduct expected, Version installed, out UpdateManifest? manifest)
    {
        manifest = null;
        if (manifestBytes.Length is 0 or > MaxManifestBytes || !IsSignedByAny(manifestBytes, signature, trustedKeys))
        {
            return UpdateVerdict.BadSignature;
        }

        // Erst jetzt, nach der Signatur, wird der Inhalt gelesen
        UpdateManifest? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<UpdateManifest>(manifestBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return UpdateVerdict.Malformed;
        }

        if (parsed is null || parsed.Format != 1 || ParseVersion(parsed.Version) is not { } version
            || parsed.Size is <= 0 or > MaxSetupBytes || !IsSha256(parsed.Sha256))
        {
            return UpdateVerdict.Malformed;
        }

        if (!string.Equals(parsed.Product, ProductName(expected), StringComparison.Ordinal))
        {
            return UpdateVerdict.WrongProduct;
        }

        if (!string.Equals(parsed.File, SetupFileName(expected, version), StringComparison.Ordinal))
        {
            return UpdateVerdict.Malformed;
        }

        if (version <= installed)
        {
            return UpdateVerdict.NotNewer;
        }

        manifest = parsed;
        return UpdateVerdict.Valid;
    }

    /// <summary>Stimmt die geladene Datei (eine Kopie am geschützten Ort) mit dem geprüften Manifest überein?</summary>
    public static async Task<bool> MatchesAsync(Stream setup, UpdateManifest manifest, CancellationToken cancellationToken)
    {
        if (setup.Length != manifest.Size)
        {
            return false;
        }

        var hash = await SHA256.HashDataAsync(setup, cancellationToken);
        return string.Equals(Convert.ToHexStringLower(hash), manifest.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>„4.0.1“ → Version; Vorabversionen („4.1.0-rc.1“) und alles andere → null (werden als Update nie angeboten).</summary>
    public static Version? ParseVersion(string? text) =>
        text is not null && StrictVersion().IsMatch(text)
        && Version.TryParse(text, out var version) ? version : null;

    /// <summary>Installierte Version ohne Vorab-Zusatz und Commit („4.0.0-dev+abc“ → 4.0.0).</summary>
    public static Version InstalledVersion(string? informationalVersion) =>
        Version.TryParse((informationalVersion ?? "0.0.0").Split('+', '-')[0], out var version) ? version : new Version(0, 0, 0);

    public static string ProductName(UpdateProduct product) => product == UpdateProduct.App ? "app" : "agent";

    /// <summary>So heißt das Setup im Release; der Name wird nie aus dem Manifest übernommen, nur verglichen.</summary>
    public static string SetupFileName(UpdateProduct product, Version version) =>
        string.Format(CultureInfo.InvariantCulture, product == UpdateProduct.App ? "Omnikit-{0}-setup.exe" : "OmnikitAgent-{0}-setup.exe", Format(version));

    public static string Format(Version version) =>
        string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", version.Major, version.Minor, version.Build);

    private static bool IsSignedByAny(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, IReadOnlyList<byte[]> trustedKeys)
    {
        if (signature.Length != 64)
        {
            return false;   // P-256 im IEEE-P1363-Format: genau 2 × 32 Bytes
        }

        foreach (var key in trustedKeys)
        {
            using var ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportSubjectPublicKeyInfo(key, out _);
            }
            catch (CryptographicException)
            {
                continue;
            }

            if (ecdsa.KeySize == 256 && ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip };

    [GeneratedRegex(@"^(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,3})\.(0|[1-9]\d{0,4})$")]
    private static partial Regex StrictVersion();
}
