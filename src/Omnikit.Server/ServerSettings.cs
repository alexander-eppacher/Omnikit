using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Services;

namespace Omnikit.Server;

/// <summary>Einstellungen aus Umgebungsvariablen bzw. appsettings.json. Geheimnisse nie im Code (Regel 6.1.7).</summary>
/// <param name="ConnectionString">ConnectionStrings__Default.</param>
/// <param name="UseSqlite">Database__Provider=Sqlite (zum Ausprobieren ohne PostgreSQL).</param>
/// <param name="AllowHttp">Server__AllowHttp=true nur zum lokalen Ausprobieren; im Betrieb immer HTTPS über Caddy.</param>
/// <param name="TrustProxy">
/// Server__TrustProxy=true, wenn ein Reverse Proxy (Caddy) davor steht und der Server selbst von außen nicht erreichbar ist:
/// Nur dann gelten dessen Angaben zu HTTPS und Absender (X-Forwarded-Proto/-For). Sonst könnte jeder diese Angaben fälschen.
/// </param>
/// <param name="InitialAdminPassword">OMNIKIT_ADMIN_PASSWORD: Passwort des ersten Admins (nur beim allerersten Start).</param>
public sealed record ServerSettings(string ConnectionString, bool UseSqlite, bool AllowHttp, bool TrustProxy, string? InitialAdminPassword)
{
    public static ServerSettings From(IConfiguration configuration) => new(
        configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings__Default fehlt."),
        string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase),
        configuration.GetValue<bool>("Server:AllowHttp"),
        configuration.GetValue<bool>("Server:TrustProxy"),
        configuration["OMNIKIT_ADMIN_PASSWORD"]);
}

/// <summary>Namen für Rechte und Grenzen.</summary>
public static class Roles
{
    public const string AdminPolicy = "Admin";
    public const string LoginRateLimit = "login";
}

internal static class Startup
{
    /// <summary>Tabellen anlegen (erster Start) und den ersten Admin.</summary>
    public static async Task PrepareDatabaseAsync(IServiceProvider services, ServerSettings settings, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await db.Database.EnsureCreatedAsync();

        var users = scope.ServiceProvider.GetRequiredService<UserService>();
        if (!await users.EnsureInitialAdminAsync(settings.InitialAdminPassword, CancellationToken.None))
        {
            logger.LogWarning(
                "Noch kein Benutzer: OMNIKIT_ADMIN_PASSWORD (mindestens {Length} Zeichen) setzen und neu starten.", UserService.MinPasswordLength);
        }
    }
}
