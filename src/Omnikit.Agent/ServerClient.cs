using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Omnikit.Shared;

namespace Omnikit.Agent;

/// <summary>Was der Agent im laufenden Betrieb vom Server braucht (als Schnittstelle, damit der Ablauf testbar ist).</summary>
public interface IAgentServer
{
    Task SendReportAsync(ReportRequest report, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentTask>> GetTasksAsync(CancellationToken cancellationToken);

    /// <returns><c>false</c>, wenn der Server die Aufgabe nicht (mehr) kennt, z. B. weil sie wegen Zeitüberschreitung schon als fehlgeschlagen gilt.</returns>
    Task<bool> SendResultAsync(int taskId, TaskResultRequest result, CancellationToken cancellationToken);
}

/// <summary>Spricht mit dem Server. Nur HTTPS; Zertifikate werden immer geprüft.</summary>
public sealed class ServerClient : IAgentServer, IDisposable
{
    private readonly HttpClient _http;

    /// <param name="caFile">
    /// Optional: Stammzertifikat des Servers (z. B. Caddys LAN-Zertifikat). Dann gilt **nur** ein Server-Zertifikat, das
    /// von genau diesem Stammzertifikat ausgestellt ist – auch keine Stelle, der Windows sonst vertraut (Pinning). Sonst könnte
    /// jede zusätzlich installierte Stelle (Virenscanner, SSL-Prüfung im Firmennetz) mitlesen und dem Agent Aufgaben geben.
    /// </param>
    public ServerClient(Uri server, string? token, string? caFile)
    {
        if (server.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Nur HTTPS.", nameof(server));
        }

        // Keine Umleitungen: Token und Berichte gehen nur an die eingetragene Adresse
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        if (caFile is not null)
        {
            var root = X509CertificateLoader.LoadCertificateFromFile(caFile);
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                IsTrusted(certificate as X509Certificate2, chain?.ChainElements.Select(element => element.Certificate) ?? [], errors, root);
        }

        _http = new HttpClient(handler) { BaseAddress = server, Timeout = TimeSpan.FromMinutes(2) };
        if (token is not null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<RegisterResponse?> RegisterAsync(string code, string computerName, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(AgentApi.Register, new RegisterRequest(code, computerName), AgentApi.JsonOptions, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // Bremse des Servers (viele PCs hinter einem Router): später noch einmal, der Code ist nicht verbraucht
            throw new HttpRequestException("Zu viele Anmeldungen", null, response.StatusCode);
        }

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RegisterResponse>(AgentApi.JsonOptions, cancellationToken)
            : null;
    }

    public async Task SendReportAsync(ReportRequest report, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(AgentApi.Report, report, AgentApi.JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<AgentTask>> GetTasksAsync(CancellationToken cancellationToken) =>
        await _http.GetFromJsonAsync<List<AgentTask>>(AgentApi.Tasks, AgentApi.JsonOptions, cancellationToken) ?? [];

    public async Task<bool> SendResultAsync(int taskId, TaskResultRequest result, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(AgentApi.TaskResult(taskId), result, AgentApi.JsonOptions, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Verwendungszweck „TLS-Server“ (Extended Key Usage).</summary>
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// Gilt nur, wenn der Name stimmt und die Kette zum mitgegebenen Stammzertifikat führt (und zu keinem anderen).
    /// Zwischenzertifikate (Caddy: Stamm → Zwischen → Server) kommen aus dem, was der Server mitschickt; sie sind nur
    /// Bausteine für den Weg, nie selbst vertrauenswürdig.
    /// </summary>
    /// <param name="presented">Vom Server mitgeschickte Zertifikate (Server-Zertifikat und Zwischenzertifikate).</param>
    internal static bool IsTrusted(X509Certificate2? certificate, IEnumerable<X509Certificate2> presented, SslPolicyErrors errors, X509Certificate2 root)
    {
        if (certificate is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) || errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            return false;
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // eigene LAN-Stelle hat keine Sperrliste
        // Keine Adressen aus einem noch ungeprüften Zertifikat abrufen (der Dienst läuft als SYSTEM)
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthentication));
        foreach (var extra in presented.Where(extra => !extra.RawDataMemory.Span.SequenceEqual(certificate.RawDataMemory.Span)))
        {
            chain.ChainPolicy.ExtraStore.Add(extra);
        }

        return chain.Build(certificate)
            && chain.ChainElements[^1].Certificate.RawDataMemory.Span.SequenceEqual(root.RawDataMemory.Span);
    }

    /// <summary>
    /// Für <c>--ca</c> taugt nur ein Stammzertifikat (selbst ausgestellt, als Zertifizierungsstelle gekennzeichnet).
    /// Wer das Zwischen- oder das Server-Zertifikat angibt, hätte sonst eine Einrichtung, die später ohne erkennbaren Grund ausfällt.
    /// </summary>
    internal static bool IsRootCertificate(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData)
        && certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(constraints => constraints.CertificateAuthority);
}
