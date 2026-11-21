using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Omnikit.Agent.Resources;
using Omnikit.Core.Updates;
using Omnikit.Core.Windows;
using Omnikit.Shared;

namespace Omnikit.Agent;

/// <summary>Vorbereitetes Selbst-Update: das Ergebnis für den Server und – wenn es etwas zu installieren gibt – das geprüfte Setup.</summary>
public sealed record AgentUpdatePlan(TaskResultRequest Result, string? Setup);

/// <summary>Aktualisiert den Agent. Getrennt in Vorbereiten und Starten, weil das Setup den Dienst beendet.</summary>
public interface IAgentUpdater
{
    /// <summary>Sucht, lädt und prüft das Update. Startet noch nichts.</summary>
    Task<AgentUpdatePlan> PrepareAsync(CancellationToken cancellationToken);

    /// <summary>Startet das geprüfte Setup. Erst aufrufen, nachdem das Ergebnis beim Server angekommen ist.</summary>
    void Start(AgentUpdatePlan plan);
}

/// <summary>
/// Aufgabe <see cref="AgentTaskKind.SelfUpdate"/>: Der Server sagt nur „aktualisiere dich“. Woher das Update kommt, welche
/// Version und ob es echt ist, entscheidet der Agent selbst – mit derselben festen Adresse und denselben eingebauten Schlüsseln
/// wie die App. Ein übernommener Server kann so keine fremde Datei einschleusen (Regel: kein Code vom Server).
/// </summary>
public sealed class AgentUpdater(ILogger<AgentUpdater> logger) : IAgentUpdater
{
    /// <summary>Geschützter Ordner für Setups (nur Administratoren und SYSTEM dürfen schreiben).</summary>
    public static string UpdateFolder => Path.Combine(AgentStore.InstallDirectory, "updates");

    public async Task<AgentUpdatePlan> PrepareAsync(CancellationToken cancellationToken)
    {
        if (UpdateSource.TrustedKeys.Count == 0)
        {
            return Fail(AgentTexts.Update_NoKeys);
        }

        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is not { } self
            || !self.StartsWith(AgentStore.InstallDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(AgentTexts.Update_NotInstalled);
        }

        try
        {
            using var http = UpdateChecker.CreateHttpClient();
            var checker = new UpdateChecker(http, UpdateSource.TrustedKeys);
            var installed = UpdateVerifier.InstalledVersion(AgentVersion.Current);
            if (await checker.FindAsync(UpdateProduct.Agent, installed, cancellationToken) is not { } offer)
            {
                return new AgentUpdatePlan(new TaskResultRequest(true, 0, AgentTexts.Update_Current), null);
            }

            // Der Agent läuft als SYSTEM: Er lädt direkt in den geschützten Ordner, den kein Benutzer verändern kann
            if (ProtectedFolder.Ensure(UpdateFolder) is var verdict && verdict != FolderVerdict.Trusted)
            {
                logger.LogError("Update-Ordner {Folder} ist nicht geschützt: {Verdict}", UpdateFolder, verdict);
                return Fail(AgentTexts.Update_FolderUntrusted);
            }

            var files = await checker.DownloadAsync(UpdateProduct.Agent, offer, UpdateFolder, null, cancellationToken);
            var message = string.Format(CultureInfo.CurrentCulture, AgentTexts.Update_Starting, offer.Manifest.Version);
            return new AgentUpdatePlan(new TaskResultRequest(true, 0, message), files.Setup);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Agent-Update konnte nicht geladen werden");
            return Fail(AgentTexts.Update_DownloadFailed);
        }
    }

    public void Start(AgentUpdatePlan plan)
    {
        if (plan.Setup is null)
        {
            return;
        }

        // Nicht warten: Das Setup hält den Dienst an (und damit diesen Prozess), ersetzt die Dateien und startet ihn neu
        var start = new ProcessStartInfo(plan.Setup) { UseShellExecute = false, WorkingDirectory = UpdateFolder };
        foreach (var argument in UpdateSource.SilentSetupArguments(Path.Combine(UpdateFolder, "setup.log")))
        {
            start.ArgumentList.Add(argument);
        }

        logger.LogInformation("Agent-Update wird gestartet: {Setup}", plan.Setup);
        using var _ = Process.Start(start);
    }

    private static AgentUpdatePlan Fail(string message) => new(new TaskResultRequest(false, 0, message), null);
}
