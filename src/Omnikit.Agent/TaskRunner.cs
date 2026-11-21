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
public sealed class TaskRunner(IWingetClient winget, ILogger<TaskRunner> logger)
{
    public async Task<TaskResultRequest> RunAsync(AgentTask task, CancellationToken cancellationToken)
    {
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
}
