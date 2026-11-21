using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Omnikit.Core.Modules;
using Omnikit.Core.Reports;
using Omnikit.Server.Services;
using Omnikit.Shared;

using static Omnikit.Server.Tests.TestCancellation;

namespace Omnikit.Server.Tests;

/// <summary>Startet den echten Server (mit SQLite-Datei) und spricht ihn wie ein Agent an.</summary>
public sealed class ServerFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"omnikit-test-{Guid.NewGuid():N}.db");

    public bool AllowHttp { get; init; } = true;

    public bool TrustProxy { get; init; }

    /// <summary>Absender-Adresse aller Anfragen (sonst hat der Testserver keine).</summary>
    public System.Net.IPAddress? RemoteAddress { get; init; }

    private sealed class RemoteAddressFilter(System.Net.IPAddress address) : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databaseFile}");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Server:AllowHttp", AllowHttp ? "true" : "false");
        builder.UseSetting("Server:TrustProxy", TrustProxy ? "true" : "false");
        builder.UseSetting("OMNIKIT_ADMIN_PASSWORD", "Test-Passwort-123");
        if (RemoteAddress is { } address)
        {
            builder.ConfigureServices(services => services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter>(new RemoteAddressFilter(address)));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_databaseFile);
    }
}

public sealed class ApiTests : IDisposable
{
    private readonly ServerFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<string> NewCodeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RegistrationService>().CreateCodeAsync("admin", null, Ct);
    }

    private static SystemReport Report() => new(
        DateTimeOffset.UtcNow, "1.0", new ReportSystemInfo("Windows 11", "24H2", "26100", "QEMU", "Standard PC", Core.SystemInfo.VmType.Qemu, false, true),
        [new ReportModule("drivers", Severity.Ok, [new Finding("Keine Gerätefehler", "ok", Severity.Ok)])]);

    [Fact]
    public async Task AgentRegistersReportsAndRunsAnApprovedTask()
    {
        using var client = _factory.CreateClient();

        var registered = await client.PostAsJsonAsync(AgentApi.Register, new RegisterRequest(await NewCodeAsync(), "VM-01"), AgentApi.JsonOptions, Ct);
        var device = await registered.Content.ReadFromJsonAsync<RegisterResponse>(AgentApi.JsonOptions, Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", device!.Token);

        var report = await client.PostAsJsonAsync(AgentApi.Report, new ReportRequest("VM-01", Report(), [new InstalledProgram("7zip.7zip", "24.09")]), AgentApi.JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var tasks = scope.ServiceProvider.GetRequiredService<TaskService>();
            await tasks.CreateAsync("admin", device.DeviceId, AgentTaskKind.Install, "VideoLAN.VLC", Ct);
            await tasks.ApproveAsync("admin", 1, Ct);
        }

        var pending = await client.GetFromJsonAsync<List<AgentTask>>(AgentApi.Tasks, AgentApi.JsonOptions, Ct);
        var task = Assert.Single(pending!);
        Assert.Equal(AgentTaskKind.Install, task.Kind);

        var result = await client.PostAsJsonAsync(AgentApi.TaskResult(task.Id), new TaskResultRequest(true, 0, "fertig"), AgentApi.JsonOptions, Ct);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    [Fact]
    public async Task SameCodeAtTheSameTimeRegistersOnlyOneDevice()
    {
        using var client = _factory.CreateClient();
        var code = await NewCodeAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            client.PostAsJsonAsync(AgentApi.Register, new RegisterRequest(code, $"PC-{i}"), AgentApi.JsonOptions, Ct)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task WithoutTokenEverythingIsRefused()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "ausgedacht");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(AgentApi.Tasks, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(AgentApi.Report, new ReportRequest("x", Report(), null), AgentApi.JsonOptions, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(AgentApi.Register, new RegisterRequest("FALSCH", "x"), AgentApi.JsonOptions, Ct)).StatusCode);
    }

    [Fact]
    public async Task AdminPagesNeedALogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Codes", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Login", Ct)).StatusCode);
    }

    [Fact]
    public async Task WithoutHttpsTheServerRefuses()
    {
        using var factory = new ServerFactory { AllowHttp = false };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(AgentApi.Tasks, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", Ct)).StatusCode);
    }

    [Fact]
    public async Task ForgedProxyHeaderDoesNotCountAsHttps()
    {
        // Ohne Reverse Proxy davor darf niemand per Kopfzeile behaupten, die Anfrage sei über HTTPS gekommen
        using var factory = new ServerFactory { AllowHttp = false };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Login");
        request.Headers.Add("X-Forwarded-Proto", "https");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task BehindATrustedProxyItsHttpsHeaderCounts()
    {
        using var factory = new ServerFactory { AllowHttp = false, TrustProxy = true };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Login");
        request.Headers.Add("X-Forwarded-Proto", "https");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Login", Ct)).StatusCode);
    }

    [Fact]
    public async Task EveryAnswerCarriesTheSecurityHeaders()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var path in new[] { "/Login", "/", AgentApi.Tasks, "/site.css" })
        {
            using var response = await client.GetAsync(path, Ct);

            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("172.18.0.3", true)]     // Caddy im Docker-Netz
    [InlineData("10.1.2.3", true)]
    [InlineData("203.0.113.5", false)]   // Adresse aus dem Internet: darf nicht „HTTPS“ behaupten
    public async Task OnlyProxiesFromPrivateNetworksCount(string remote, bool accepted)
    {
        using var factory = new ServerFactory { AllowHttp = false, TrustProxy = true, RemoteAddress = System.Net.IPAddress.Parse(remote) };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Login");
        request.Headers.Add("X-Forwarded-Proto", "https");

        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, (await client.SendAsync(request, Ct)).StatusCode);
    }
}
