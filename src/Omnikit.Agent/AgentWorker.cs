using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omnikit.Shared;

namespace Omnikit.Agent;

/// <summary>
/// Der Dienst selbst: fragt jede Minute nach freigegebenen Aufgaben und schickt alle 30 Minuten einen Bericht.
/// Fällt der Server aus, versucht er es einfach beim nächsten Mal wieder.
/// </summary>
public sealed class AgentWorker(AgentCycle cycle, ILogger<AgentWorker> logger) : BackgroundService
{
    public static readonly TimeSpan TaskInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ReportInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextReport = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(TaskInterval);
        do
        {
            try
            {
                if (DateTimeOffset.UtcNow >= nextReport)
                {
                    await cycle.SendReportAsync(stoppingToken);
                    nextReport = DateTimeOffset.UtcNow + ReportInterval;
                }

                await cycle.RunTasksAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Server nicht erreichbar – nächster Versuch in einer Minute");
            }
#pragma warning disable CA1031 // Jeder andere Fehler (z. B. unerwartete Antwort) darf den Dienst nicht für immer anhalten.
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Fehler im Agent – nächster Versuch in einer Minute");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Ein Durchgang: Bericht schicken, Aufgaben holen, ausführen, Ergebnis melden.</summary>
public sealed class AgentCycle(IAgentServer server, ReportBuilder reports, TaskLoop tasks, ILogger<AgentCycle> logger)
{
    public async Task SendReportAsync(CancellationToken cancellationToken)
    {
        await server.SendReportAsync(await reports.BuildAsync(cancellationToken), cancellationToken);
        logger.LogInformation("Bericht gesendet");
    }

    public async Task RunTasksAsync(CancellationToken cancellationToken)
    {
        // Nach Installationen gleich einen neuen Bericht, damit der Server die neue Programmliste kennt
        if (await tasks.RunAsync(cancellationToken) > 0)
        {
            await SendReportAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Holt Aufgaben einzeln ab: ausführen, Ergebnis melden, nächste holen – bis keine mehr da ist.
/// Das Ergebnis wird bei Netzfehlern bis zu dreimal gesendet; eine Installation wird nie wiederholt.
/// </summary>
public sealed class TaskLoop(IAgentServer server, TaskRunner runner, ILogger<TaskLoop> logger, TimeSpan retryDelay)
{
    /// <summary>Obergrenze je Durchgang, falls der Server immer neue Aufgaben liefert; der Rest folgt im nächsten.</summary>
    public const int MaxTasksPerCycle = 20;

    public const int ResultAttempts = 3;

    /// <returns>Wie viele Aufgaben ausgeführt wurden.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var done = 0;
        while (done < MaxTasksPerCycle)
        {
            var tasks = await server.GetTasksAsync(cancellationToken);
            if (tasks.Count == 0)
            {
                break;
            }

            foreach (var task in tasks)
            {
                var result = await runner.RunAsync(task, cancellationToken);
                done++;
                if (await SendResultAsync(task.Id, result, cancellationToken))
                {
                    logger.LogInformation("Aufgabe {Id} fertig: {Success} {Message}", task.Id, result.Success, result.Message);
                }
                else
                {
                    logger.LogWarning("Aufgabe {Id}: Der Server kennt sie nicht mehr (zu spät gemeldet?)", task.Id);
                }

                // Selbst-Update: Erst jetzt, mit gemeldetem Ergebnis, starten – das Setup beendet gleich den Dienst
                if (runner.StartPendingUpdate())
                {
                    return done;
                }
            }
        }

        return done;
    }

    private async Task<bool> SendResultAsync(int taskId, TaskResultRequest result, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await server.SendResultAsync(taskId, result, cancellationToken);
            }
            catch (Exception ex) when (attempt < ResultAttempts
                && (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)))
            {
                logger.LogWarning(ex, "Ergebnis von Aufgabe {Id} nicht angekommen – Versuch {Attempt} von {Max}", taskId, attempt + 1, ResultAttempts);
                await Task.Delay(retryDelay * attempt, cancellationToken);
            }
        }
    }
}
