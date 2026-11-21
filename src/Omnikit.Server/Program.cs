using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server;
using Omnikit.Server.Api;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Security;
using Omnikit.Server.Services;
using Omnikit.Shared;

// Oberfläche auf Deutsch: Datum und Zahlen im deutschen Format
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

var builder = WebApplication.CreateBuilder(args);
var settings = ServerSettings.From(builder.Configuration);

// Datenbank: PostgreSQL im Betrieb (docker compose), SQLite zum Ausprobieren und in Tests
builder.Services.AddDbContext<ServerDbContext>(options =>
{
    if (settings.UseSqlite)
    {
        options.UseSqlite(settings.ConnectionString);
    }
    else
    {
        options.UseNpgsql(settings.ConnectionString);
    }
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuditLog>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddHostedService<LostTaskSweeper>();

// Gleiche JSON-Einstellungen wie der Agent (Aufzählungen als Text)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Encoder = AgentApi.JsonOptions.Encoder;
});
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = AgentApi.MaxReportBytes);

// Anmeldung an der Verwaltung: Cookie nur über HTTPS, nicht von fremden Seiten mitgeschickt
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Denied";
    options.Cookie.Name = "Omnikit.Login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = settings.AllowHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // Bei jeder Anfrage prüfen, ob es den Benutzer noch gibt und Rolle und Passwort noch stimmen
    // (gelöscht oder Passwort geändert = sofort abgemeldet)
    options.Events.OnValidatePrincipal = async context =>
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<ServerDbContext>();
        var name = context.Principal?.Identity?.Name;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserName == name);
        if (!SessionStamp.IsStillValid(context.Principal, user))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});
builder.Services.AddAuthorizationBuilder().AddPolicy(Roles.AdminPolicy, policy => policy.RequireRole(nameof(UserRole.Admin)));

// Alle Seiten nur angemeldet; Codes, Benutzer und Audit-Log nur für Admins (Rechte werden auf dem Server geprüft)
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Denied");
    options.Conventions.AuthorizePage("/Codes", Roles.AdminPolicy);
    options.Conventions.AuthorizePage("/Users", Roles.AdminPolicy);
    options.Conventions.AuthorizePage("/Audit", Roles.AdminPolicy);
    options.Conventions.AddPageApplicationModelConvention("/Login",
        model => model.EndpointMetadata.Add(new Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute(Roles.LoginRateLimit)));
});

// Gegen Durchprobieren: pro Minute und Adresse höchstens 10 Registrierungen und 20 Aufrufe der Login-Seite
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (var (name, limit) in new[] { (AgentEndpoints.RegisterRateLimit, 10), (Roles.LoginRateLimit, 20) })
    {
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "?",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1) }));
    }
});

// Hinter Caddy (docker compose, Port nicht nach außen freigegeben): dessen Angaben zu HTTPS und Adresse übernehmen – nur mit
// Server__TrustProxy. Caddy hat im Docker-Netz keine feste Adresse; deshalb gelten alle privaten Netze (und dieser Rechner) als Proxy.
// Kommt eine Anfrage aus dem Internet direkt an, werden ihre Angaben trotzdem ignoriert (Schutz bei falscher Einrichtung).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var network in ProxyNetworks.Private)
    {
        options.KnownIPNetworks.Add(network);
    }
});

var app = builder.Build();

if (settings.TrustProxy)
{
    app.UseForwardedHeaders();
}

app.Use(SecurityHeaders.AddAsync);
if (!settings.AllowHttp)
{
    // HTTPS erzwingen (Regel 6.1.6): Anfragen ohne HTTPS werden abgelehnt, nicht umgeleitet
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps && context.Request.Path != "/health")
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(ServerTexts.Error_HttpsOnly);
            return;
        }

        await next();
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "ok");
app.MapAgentEndpoints();
app.MapRazorPages();

await Startup.PrepareDatabaseAsync(app.Services, settings, app.Logger);
await app.RunAsync();

/// <summary>Für die Tests (WebApplicationFactory).</summary>
public partial class Program;
