using System.Text;
using System.Xml.Linq;

namespace Omnikit.Modules.StickBuilder;

/// <summary>
/// WLAN-Profil im Format von Windows („WLANProfile“), das während der Installation mit
/// <c>netsh wlan add profile</c> eingetragen wird. Aufbau nach der Microsoft-Dokumentation „WLAN_profile Schema“.
/// </summary>
public static class WifiProfile
{
    private static readonly XNamespace W = "http://www.microsoft.com/networking/WLAN/profile/v1";

    /// <summary>Netzwerkname: 1–32 Byte (UTF-8), ohne Steuerzeichen.</summary>
    public static bool IsValidName(string name) =>
        name.Length > 0 && Encoding.UTF8.GetByteCount(name) <= 32 && !name.Any(char.IsControl);

    /// <summary>Leer (offenes WLAN), 8–63 druckbare ASCII-Zeichen oder ein Schlüssel aus 64 Hex-Ziffern.</summary>
    public static bool IsValidPassword(string password) =>
        password.Length == 0
        || (password.Length is >= 8 and <= 63 && password.All(c => c is >= ' ' and <= '~'))
        || (password.Length == 64 && password.All(char.IsAsciiHexDigit));

    /// <summary>
    /// WPA2-Personal mit AES: Damit verbinden sich auch Router, die WPA2 und WPA3 gemischt anbieten.
    /// Ein Router nur mit WPA3 braucht im Setup die WLAN-Seite.
    /// </summary>
    public static string Create(string name, string password)
    {
        var hex = Convert.ToHexString(Encoding.UTF8.GetBytes(name));
        var security = password.Length == 0
            ? new XElement(W + "security", AuthEncryption("open", "none"))
            : new XElement(W + "security", AuthEncryption("WPA2PSK", "AES"),
                new XElement(W + "sharedKey",
                    new XElement(W + "keyType", password.Length == 64 ? "networkKey" : "passPhrase"),
                    new XElement(W + "protected", "false"),
                    new XElement(W + "keyMaterial", password)));

        var profile = new XElement(W + "WLANProfile",
            new XElement(W + "name", name),
            new XElement(W + "SSIDConfig",
                // Hex für Namen mit Umlauten oder Sonderzeichen, der Name zum Lesen
                new XElement(W + "SSID", new XElement(W + "hex", hex), new XElement(W + "name", name)),
                // „true“ würde den Namen überall aktiv suchen und damit verraten; versteckte WLANs sind selten
                new XElement(W + "nonBroadcast", "false")),
            new XElement(W + "connectionType", "ESS"),
            new XElement(W + "connectionMode", "auto"),
            new XElement(W + "MSM", security));

        // Mit Kopfzeile wie die Profile, die Windows selbst exportiert (netsh wlan export profile)
        return "<?xml version=\"1.0\"?>" + Environment.NewLine + profile.ToString(SaveOptions.None);
    }

    private static XElement AuthEncryption(string authentication, string encryption) =>
        new(W + "authEncryption",
            new XElement(W + "authentication", authentication),
            new XElement(W + "encryption", encryption),
            new XElement(W + "useOneX", "false"));
}
