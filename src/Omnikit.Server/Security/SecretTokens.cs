using System.Security.Cryptography;
using System.Text;

namespace Omnikit.Server.Security;

/// <summary>
/// Zufällige Geheimnisse (Geräte-Tokens, Registrierungscodes) und ihr Hash.
/// Gespeichert wird nur der SHA-256-Hash: Wer die Datenbank liest, kann sich damit nicht als Gerät ausgeben.
/// Ein schneller Hash reicht, weil die Werte zufällig und lang sind (kein Passwort, das man raten könnte).
/// </summary>
public static class SecretTokens
{
    // Ohne leicht verwechselbare Zeichen (0/O, 1/I/L), damit man Codes abtippen kann
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Geräte-Token: 256 Bit, Base64url.</summary>
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Registrierungscode zum Abtippen, z. B. „K7QM-3XRP-…“: 4 Blöcke à 5 Zeichen (≈ 99 Bit).</summary>
    public static string NewCode()
    {
        var chars = Enumerable.Range(0, 20).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray();
        return string.Join('-', chars.Chunk(5).Select(block => new string(block)));
    }

    /// <summary>Codes ohne Bindestriche und Leerzeichen, in Großbuchstaben – so ist das Abtippen fehlertolerant.</summary>
    public static string NormalizeCode(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
