namespace Omnikit.Server.Services;

/// <summary>
/// Prüft alle 5 Minuten, ob Aufgaben ohne Rückmeldung abgelaufen sind (<see cref="AgentService.ExpireLostTasksAsync"/>) –
/// auch für Geräte, die sich nie mehr melden.
/// </summary>
public sealed class LostTaskSweeper(IServiceScopeFactory scopes, ILogger<LostTaskSweeper> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<AgentService>().ExpireLostTasksAsync(null, stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("{Count} Aufgaben ohne Rückmeldung als fehlgeschlagen markiert", expired);
                }
            }
#pragma warning disable CA1031 // Ein Fehler (z. B. Datenbank kurz weg) darf die Prüfung nicht für immer beenden.
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Prüfung auf abgelaufene Aufgaben fehlgeschlagen");
            }
        }
    }
}
