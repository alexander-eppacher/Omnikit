using System.Globalization;
using System.Diagnostics;
using System.Security.Principal;

using Omnikit.Agent.Resources;

namespace Omnikit.Agent;

/// <summary>
/// Richtet den Windows-Dienst ein bzw. entfernt ihn (mit sc.exe aus System32, Argumente als Liste).
/// Der Agent wird vorher nach „C:\Program Files\Omnikit Agent“ kopiert: Der Dienst läuft als SYSTEM, deshalb darf
/// seine .exe nur dort liegen, wo normale Benutzer sie nicht austauschen können (nicht z. B. in „Downloads“).
/// </summary>
public static class ServiceInstaller
{
    public const string ServiceName = "OmnikitAgent";

    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static async Task<string> InstallAsync()
    {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException("Pfad des Agents unbekannt.");
        Directory.CreateDirectory(AgentStore.InstallDirectory);
        var target = Path.Combine(AgentStore.InstallDirectory, "Omnikit.Agent.exe");
        if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            await StopAsync(); // eine ältere Version könnte die Datei gerade benutzen
            File.Copy(source, target, overwrite: true);
        }

        if (await ScAsync("query", ServiceName) != 0)
        {
            Require(await ScAsync("create", ServiceName, "binPath=", $"\"{target}\" run", "start=", "delayed-auto", "DisplayName=", "Omnikit Agent"), "create");
            await ScAsync("description", ServiceName, AgentTexts.Service_Description);
        }

        // Bei einem Absturz nach einer Minute neu starten (auch bei schon vorhandenem Dienst setzen)
        Require(await ScAsync("failure", ServiceName, "reset=", "86400", "actions=", "restart/60000/restart/60000/restart/300000"), "failure");
        Require(await ScAsync("start", ServiceName), "start", ErrorServiceAlreadyRunning);
        return target;
    }

    public static async Task UninstallAsync()
    {
        await StopAsync();
        Require(await ScAsync("delete", ServiceName), "delete", ErrorServiceDoesNotExist);
    }

    // Rückgabecodes von sc.exe, die kein Fehler sind
    private const int ErrorServiceAlreadyRunning = 1056;
    private const int ErrorServiceNotActive = 1062;
    private const int ErrorServiceDoesNotExist = 1060;

    /// <exception cref="InvalidOperationException">sc.exe hat einen Fehler gemeldet.</exception>
    private static void Require(int exitCode, string step, params int[] alsoFine)
    {
        if (exitCode != 0 && !alsoFine.Contains(exitCode))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, AgentTexts.Service_StepFailed, step, exitCode));
        }
    }

    /// <summary>Dienst anhalten und warten, bis er wirklich beendet ist (höchstens 30 Sekunden).</summary>
    private static async Task StopAsync()
    {
        Require(await ScAsync("stop", ServiceName), "stop", ErrorServiceNotActive, ErrorServiceDoesNotExist);
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var service = new System.ServiceProcess.ServiceController(ServiceName);
        try
        {
            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException)
        {
            // Dienst gibt es nicht (mehr)
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            throw new InvalidOperationException(AgentTexts.Service_StopTimeout);
        }
    }

    private static async Task<int> ScAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("sc.exe startet nicht.");
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return process.ExitCode;
    }
}
