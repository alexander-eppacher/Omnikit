using System.Net.Http.Headers;

namespace Omnikit.Core.Updates;

/// <summary>Ein geprüftes Update-Angebot samt den Original-Bytes, damit der Hilfsprozess es selbst noch einmal prüfen kann.</summary>
public sealed record UpdateOffer(UpdateManifest Manifest, Version Version, byte[] ManifestBytes, byte[] Signature);

/// <summary>
/// Fragt die Release-Seite nach einem neueren Update und lädt das Setup. Läuft ohne Adminrechte; ausgeführt wird erst nach
/// einer zweiten Prüfung im Hilfsprozess. Die Anfrage enthält keine Kennung des Geräts (nur „Omnikit“ als Programmname).
/// </summary>
public sealed class UpdateChecker(HttpClient http, IReadOnlyList<byte[]> trustedKeys)
{
    /// <summary>Gemeinsamer HttpClient für Update-Anfragen: feste Zeitgrenze, keine Cookies, neutraler Programmname.</summary>
    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = true, MaxAutomaticRedirections = 5 })
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Omnikit", null));
        return client;
    }

    /// <returns>Ein echtes, neueres Update – sonst <c>null</c> (auch ohne Internet oder bei kaputter Antwort).</returns>
    public async Task<UpdateOffer?> FindAsync(UpdateProduct product, Version installed, CancellationToken cancellationToken)
    {
        var manifest = await GetSmallAsync(UpdateSource.LatestManifest(product), UpdateVerifier.MaxManifestBytes, cancellationToken);
        var signature = manifest is null ? null : await GetSmallAsync(UpdateSource.LatestSignature(product), 64, cancellationToken);
        if (manifest is null || signature is null)
        {
            return null;
        }

        return UpdateVerifier.Verify(manifest, signature, trustedKeys, product, installed, out var parsed) == UpdateVerdict.Valid
            ? new UpdateOffer(parsed!, UpdateVerifier.ParseVersion(parsed!.Version)!, manifest, signature)
            : null;
    }

    /// <summary>
    /// Lädt das Setup nach <paramref name="folder"/> (unfertig als „.part“, nie größer als angekündigt) und prüft Größe und Prüfsumme.
    /// Legt Manifest und Signatur daneben, damit der Hilfsprozess sie selbst prüfen kann.
    /// </summary>
    /// <returns>Pfade von Manifest, Signatur und Setup.</returns>
    public async Task<(string Manifest, string Signature, string Setup)> DownloadAsync(
        UpdateProduct product, UpdateOffer offer, string folder, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        var setup = Path.Combine(folder, offer.Manifest.File);
        var partial = setup + ".part";

        using (var response = await http.GetAsync(UpdateSource.Setup(product, offer.Version), HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > offer.Manifest.Size)
                {
                    throw new InvalidDataException("Das Setup ist größer als angekündigt.");
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report((double)total / offer.Manifest.Size);
            }
        }

        await using (var check = File.OpenRead(partial))
        {
            if (!await UpdateVerifier.MatchesAsync(check, offer.Manifest, cancellationToken))
            {
                throw new InvalidDataException("Das geladene Setup passt nicht zur Prüfsumme.");
            }
        }

        File.Move(partial, setup, overwrite: true);
        var manifest = Path.Combine(folder, UpdateSource.ManifestName(product));
        await File.WriteAllBytesAsync(manifest, offer.ManifestBytes, cancellationToken);
        await File.WriteAllBytesAsync(manifest + ".sig", offer.Signature, cancellationToken);
        return (manifest, manifest + ".sig", setup);
    }

    /// <summary>Kleine Datei laden, aber nie mehr als <paramref name="limit"/> Bytes lesen. Netzfehler → <c>null</c>.</summary>
    private async Task<byte[]?> GetSmallAsync(Uri address, int limit, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > limit)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[limit + 1];
            var length = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
            return length > limit ? null : buffer[..length];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }
}
