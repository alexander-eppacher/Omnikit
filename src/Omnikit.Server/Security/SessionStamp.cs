using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Omnikit.Server.Data;

namespace Omnikit.Server.Security;

/// <summary>
/// Bindet eine Anmeldung an das Passwort, mit dem sie entstanden ist: Wird das Passwort geändert,
/// sind alle älteren Anmeldungen (z. B. an einem anderen PC) sofort ungültig.
/// </summary>
public static class SessionStamp
{
    public const string ClaimType = "omnikit:stamp";

    /// <summary>Kurzer Fingerabdruck des Passwort-Hashes – das Passwort selbst lässt sich daraus nicht ableiten.</summary>
    public static string Of(User user) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash)))[..16];

    /// <summary>Der angemeldete Benutzer, wie er im Anmelde-Cookie steht: Name, Rolle und Fingerabdruck.</summary>
    public static ClaimsPrincipal Principal(User user)
    {
        Claim[] claims = [new(ClaimTypes.Name, user.UserName), new(ClaimTypes.Role, user.Role.ToString()), new(ClaimType, Of(user))];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>Gilt die Anmeldung noch? Nur wenn es den Benutzer gibt und Rolle und Passwort seit der Anmeldung gleich geblieben sind.</summary>
    public static bool IsStillValid(ClaimsPrincipal? principal, User? user) =>
        principal is not null
        && user is not null
        && principal.IsInRole(user.Role.ToString())
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(principal.FindFirstValue(ClaimType) ?? string.Empty),
            Encoding.UTF8.GetBytes(Of(user)));
}
