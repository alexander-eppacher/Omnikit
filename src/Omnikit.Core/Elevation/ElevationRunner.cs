using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Omnikit.Core.Elevation;

/// <summary>
/// Startet die eigene Omnikit.exe noch einmal mit Adminrechten im Hilfsmodus (ohne Fenster)
/// und empfängt das Ergebnis über eine Named Pipe.
/// Windows zeigt dabei die Benutzerkontensteuerung (UAC) an. Das lässt sich nicht umgehen und ist so gewollt.
/// </summary>
public sealed class ElevationRunner : IElevationRunner
{
    // Lange genug, damit der Nutzer in Ruhe ein Admin-Passwort eingeben kann.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(90);

    // Windows-Fehlercode, wenn der Nutzer in der UAC-Abfrage "Nein" klickt.
    private const int ErrorCancelled = 1223;

    private readonly IEnumerable<IElevatedOperation> _operations;
    private readonly ILogger<ElevationRunner> _logger;

    public ElevationRunner(IEnumerable<IElevatedOperation> operations, ILogger<ElevationRunner> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    public async Task<ElevationResult> RunAsync(
        string operationId, IReadOnlyList<string> arguments, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var operation = _operations.FirstOrDefault(op => op.Id == operationId);
        if (operation is null)
        {
            _logger.LogError("Unbekannte Aktion {OperationId}", operationId);
            return new ElevationResult(ElevationStatus.Failed);
        }

        if (Environment.IsPrivilegedProcess)
        {
            return await RunInProcessAsync(operation, arguments, progress, cancellationToken);
        }

        var executablePath = Environment.ProcessPath;
        if (!OperatingSystem.IsWindows() || executablePath is null)
        {
            return new ElevationResult(ElevationStatus.NotSupported);
        }

        var pipeName = ElevatedOperationHost.CreatePipeName();

        await using var pipe = CreatePipe(pipeName);

        // Zeitgrenze: Zeit für die Windows-Abfrage plus die erlaubte Dauer der Aktion
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout + operation.Timeout);

        // Schon vor dem Start auf die Verbindung warten, damit keine Antwort verloren geht.
        var connectTask = pipe.WaitForConnectionAsync(timeout.Token);

        Process? helper;
        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas", // "Als Administrator ausführen"
                ArgumentList = { ElevatedOperationHost.Argument, operationId, pipeName },
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            helper = Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            _logger.LogInformation("Adminrechte wurden vom Nutzer nicht erteilt");
            await timeout.CancelAsync();
            await IgnoreCancellationAsync(connectTask);
            return new ElevationResult(ElevationStatus.Cancelled);
        }

        using (helper)
        {
            if (helper is null)
            {
                _logger.LogError("Hilfsprozess konnte nicht gestartet werden");
                return new ElevationResult(ElevationStatus.Failed);
            }

            return await ReceiveAsync(pipe, connectTask, helper, progress, timeout);
        }
    }

    private async Task<ElevationResult> ReceiveAsync(
        NamedPipeServerStream pipe, Task connectTask, Process helper, IProgress<string>? progress, CancellationTokenSource timeout)
    {
        try
        {
            // Entweder der Hilfsprozess verbindet sich, oder er beendet sich vorher (dann ist etwas schiefgegangen).
            await Task.WhenAny(connectTask, helper.WaitForExitAsync(timeout.Token));
            if (!connectTask.IsCompletedSuccessfully)
            {
                _logger.LogError("Hilfsprozess hat sich nicht verbunden (Exit-Code: {ExitCode})", helper.HasExited ? helper.ExitCode : null);
                await timeout.CancelAsync();
                await IgnoreCancellationAsync(connectTask);
                return new ElevationResult(ElevationStatus.Failed);
            }

            // Zeile für Zeile: erst Fortschritt, zum Schluss das Ergebnis
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                var message = JsonSerializer.Deserialize<ElevatedMessage>(line, ElevatedMessage.JsonOptions);
                if (message?.Progress is { } text)
                {
                    progress?.Report(text);
                }
                else if (message?.Success is bool success)
                {
                    return new ElevationResult(success && message.Payload is not null ? ElevationStatus.Success : ElevationStatus.Failed, message.Payload);
                }
            }

            _logger.LogError("Hilfsprozess hat ohne Ergebnis aufgehört");
            return new ElevationResult(ElevationStatus.Failed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Zeitüberschreitung beim Warten auf den Hilfsprozess");
            return new ElevationResult(ElevationStatus.Failed);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogError(ex, "Antwort des Hilfsprozesses konnte nicht gelesen werden");
            return new ElevationResult(ElevationStatus.Failed);
        }
    }

    /// <summary>Läuft die App schon als Admin, braucht es keinen Hilfsprozess. Fehler werden genauso behandelt wie im Hilfsprozess.</summary>
    private async Task<ElevationResult> RunInProcessAsync(
        IElevatedOperation operation, IReadOnlyList<string> arguments, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var message = await Task.Run(
            () => ElevatedOperationHost.RunOperationAsync(operation, arguments, progress ?? new Progress<string>(), _logger, cancellationToken),
            CancellationToken.None);
        return new ElevationResult(message.Success == true && message.Payload is not null ? ElevationStatus.Success : ElevationStatus.Failed, message.Payload);
    }

    /// <summary>
    /// Pipe für die Antworten des Hilfsprozesses. Verbinden dürfen sich nur dieser Windows-Benutzer und Administratoren:
    /// Bei einem Standardkonto erteilt ein *anderes* (Admin-)Konto in der UAC-Abfrage die Rechte, und der Hilfsprozess
    /// läuft dann unter diesem Konto. „FirstPipeInstance“: Gibt es den Namen schon, schlägt das Anlegen fehl, statt sich anzuhängen.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreatePipe(string pipeName)
    {
        var security = new PipeSecurity();
        using (var identity = WindowsIdentity.GetCurrent())
        {
            security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 0, 0, security);
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Erwartet: Das Warten wurde absichtlich abgebrochen.
        }
    }
}
