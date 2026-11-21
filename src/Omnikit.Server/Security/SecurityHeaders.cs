namespace Omnikit.Server.Security;

/// <summary>
/// Schutz-Angaben für den Browser bei jeder Antwort – auch wenn der Server einmal ohne Caddy läuft.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Die Seiten laden nur eigene Dateien: keine fremden Skripte, keine Einbettung in fremde Seiten,
    /// Formulare gehen nur an diesen Server. „unsafe-inline“ gilt nur für Farben/Abstände direkt im HTML.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; form-action 'self'; base-uri 'none'";

    public static Task AddAsync(HttpContext context, RequestDelegate next)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";       // Dateityp nicht erraten
        headers.XFrameOptions = "DENY";                // nicht in fremde Seiten einbetten (Clickjacking)
        headers["Referrer-Policy"] = "no-referrer";    // Adressen der Verwaltung nicht an andere Seiten weitergeben
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        return next(context);
    }
}
