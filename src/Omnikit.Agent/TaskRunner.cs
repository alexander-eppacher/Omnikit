using Microsoft.Extensions.Logging;
using Omnikit.Modules.Apps.Winget;
using Omnikit.Shared;

using Omnikit.Agent.Resources;

namespace Omnikit.Agent;

/// <summary>
/// Führt eine Aufgabe vom Server aus. Nur die feste Liste aus <see cref="AgentTaskKind"/> und nur mit gültiger
/// winget-Paket-ID – der Server kann keine beliebigen Befehle schicken (Regel 6.1.6). Geprüft wird hier noch einmal,
/// auch wenn der Server schon prüft: Der Agent vertraut nur dem, was er selbst geprüft hat.
/// </summary>
public sealed class TaskRunner(IWingetClient winget, IAgentUpdater updater, ILogger<TaskRunner> logger)
{
    private AgentUpdatePlan? _pendingUpdate;

    /// <summary>Was dieser Agent ausführen kann; wird mit jedem Bericht gemeldet, damit der Server nur das anbietet.</summary>
    public static IReadOnlyList<AgentTaskKind> Supported { get; } = [AgentTaskKind.Install, AgentTaskKind.Upgrade, AgentTaskKind.SelfUpdate];

    public async Task<TaskResultRequest> RunAsync(AgentTask task, CancellationToken cancellationToken)
    {
        if (task.Kind == AgentTaskKind.SelfUpdate)
        {
            // Erst vorbereiten und prüfen; gestartet wird erst, wenn der Server das Ergebnis hat (StartPendingUpdate)
            logger.LogInformation("Aufgabe {Id}: Agent aktualisieren", task.Id);
            var plan = await updater.PrepareAsync(cancellationToken);
            _pendingUpdate = plan.Setup is null ? null : plan;
            return plan.Result;
        }

        if (!PackageId.IsValid(task.PackageId))
        {
            return new TaskResultRequest(false, 0, AgentTexts.Task_InvalidPackage);
        }

        Func<string, CancellationToken, Task<PackageResult>>? action = task.Kind switch
        {
            AgentTaskKind.Install => winget.InstallAsync,
            AgentTaskKind.Upgrade => winget.UpgradeAsync,
            _ => null,
        };

        if (action is null)
        {
            return new TaskResultRequest(false, 0, AgentTexts.Task_Unknown);
        }

        logger.LogInformation("Aufgabe {Id}: {Kind} {Package}", task.Id, task.Kind, task.PackageId);
        try
        {
            var result = await action(task.PackageId, cancellationToken);
            return new TaskResultRequest(result.IsSuccess, result.ExitCode, OutcomeText.For(result.Outcome));
        }
        catch (WingetMissingException)
        {
            return new TaskResultRequest(false, 0, AgentTexts.Task_NoWinget);
        }
        catch (WingetException ex)
        {
            return new TaskResultRequest(false, 0, OutcomeText.For(ex.Outcome));
        }
    }

    /// <summary>Startet ein vorbereitetes Selbst-Update (nach dem Melden des Ergebnisses).</summary>
    /// <returns><c>true</c>, wenn eins gestartet wurde – dann beendet das Setup gleich den Dienst.</returns>
    public bool StartPendingUpdate()
    {
        if (_pendingUpdate is not { } plan)
        {
            return false;
        }

        _pendingUpdate = null;
        updater.Start(plan);
        return true;
    }
}
