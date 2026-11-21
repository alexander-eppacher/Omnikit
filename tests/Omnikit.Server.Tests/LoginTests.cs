using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using static Omnikit.Server.Tests.TestCancellation;

namespace Omnikit.Server.Tests;

/// <summary>Anmeldung an der Verwaltung über echte HTTP-Anfragen (wie ein Browser, mit Cookies und Formular-Schutz).</summary>
public sealed partial class LoginTests : IDisposable
{
    private const string AdminPassword = "Test-Passwort-123";
    private readonly ServerFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Browser() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Lädt die Seite, liest das versteckte Feld gegen Formular-Fälschung und schickt das Formular ab.</summary>
    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient browser, string path, Dictionary<string, string> fields)
    {
        var page = await browser.GetStringAsync(path, Ct);
        fields["__RequestVerificationToken"] = TokenField().Match(page).Groups[1].Value;
        using var content = new FormUrlEncodedContent(fields);
        return await browser.PostAsync(path, content, Ct);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient browser, string password) =>
        PostFormAsync(browser, "/Login", new() { ["UserName"] = "admin", ["Password"] = password });

    [Fact]
    public async Task RightPasswordOpensTheAdminPages()
    {
        using var browser = Browser();

        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(browser, AdminPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Codes", Ct)).StatusCode);

        // Das Audit-Log zeigt Sätze, obwohl nur Codes und Werte gespeichert sind (erster Start + diese Anmeldung)
        var audit = WebUtility.HtmlDecode(await browser.GetStringAsync("/Audit", Ct));
        Assert.Contains("Benutzer „admin“ angelegt (Admin)", audit, StringComparison.Ordinal);
        Assert.Contains("Angemeldet", audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongPasswordStaysOnTheLoginPage()
    {
        using var browser = Browser();

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(browser, "falsches Passwort")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Codes", Ct)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var entry = Assert.Single(scope.ServiceProvider.GetRequiredService<Omnikit.Server.Data.ServerDbContext>().Audit, e => e.Action == "user.login.failed");
        Assert.Equal("admin", Omnikit.Server.Services.AuditText.Deserialize(entry.Values)["user"]);
    }

    [Fact]
    public async Task ChangingThePasswordEndsAllOtherSessions()
    {
        using var here = Browser();
        using var elsewhere = Browser();
        await LoginAsync(here, AdminPassword);
        await LoginAsync(elsewhere, AdminPassword);
        Assert.Equal(HttpStatusCode.OK, (await elsewhere.GetAsync("/Codes", Ct)).StatusCode);

        var changed = await PostFormAsync(here, "/Account", new() { ["current"] = AdminPassword, ["next"] = "ein ganz neues Passwort" });

        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        // Wer das Passwort geändert hat, bleibt angemeldet – die andere Anmeldung (z. B. ein Angreifer mit dem alten Passwort) nicht
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/Codes", Ct)).StatusCode);
        var other = await elsewhere.GetAsync("/Codes", Ct);
        Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);
        Assert.Contains("/Login", other.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormsWithoutTheHiddenTokenAreRejected()
    {
        using var browser = Browser();
        await LoginAsync(browser, AdminPassword);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["current"] = AdminPassword, ["next"] = "ein ganz neues Passwort" });

        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Account", content, Ct)).StatusCode);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenField();
}
