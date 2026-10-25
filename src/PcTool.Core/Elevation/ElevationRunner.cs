using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PcTool.Core.Elevation;

/// <summary>
/// Startet die eigene PcTool.exe noch einmal mit Adminrechten im Hilfsmodus (ohne Fenster)
/// und empfängt das Ergebnis über eine Named Pipe.
/// Windows zeigt dabei die Benutzerkontensteuerung (UAC) an. Das lässt sich nicht umgehen und ist so gewollt.
/// </summary>
public sealed class ElevationRunner : IElevationRunner
{
    // Lange genug, damit der Nutzer in Ruhe ein Admin-Passwort eingeben kann.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    // Windows-Fehlercode, wenn der Nutzer in der UAC-Abfrage "Nein" klickt.
    private const int ErrorCancelled = 1223;

    private readonly IEnumerable<IElevatedOperation> _operations;
    private readonly ILogger<ElevationRunner> _logger;

    public ElevationRunner(IEnumerable<IElevatedOperation> operations, ILogger<ElevationRunner> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    public async Task<ElevationResult> RunAsync(string operationId, CancellationToken cancellationToken)
    {
        if (Environment.IsPrivilegedProcess)
        {
            return await RunInProcessAsync(operationId, cancellationToken);
        }

        var executablePath = Environment.ProcessPath;
        if (!OperatingSystem.IsWindows() || executablePath is null)
        {
            return new ElevationResult(ElevationStatus.NotSupported);
        }

        var pipeName = ElevatedOperationHost.CreatePipeName();

        // CurrentUserOnly: Nur Prozesse desselben Windows-Benutzers dürfen sich verbinden.
        await using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        // Schon vor dem Start auf die Verbindung warten, damit keine Antwort verloren geht.
        var connectTask = pipe.WaitForConnectionAsync(timeout.Token);

        Process? helper;
        try
        {
            helper = Process.Start(new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas", // "Als Administrator ausführen"
                ArgumentList = { ElevatedOperationHost.Argument, operationId, pipeName },
            });
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

            return await ReceiveAsync(pipe, connectTask, helper, timeout);
        }
    }

    private async Task<ElevationResult> ReceiveAsync(
        NamedPipeServerStream pipe, Task connectTask, Process helper, CancellationTokenSource timeout)
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

            using var reader = new StreamReader(pipe, Encoding.UTF8);
            var json = await reader.ReadToEndAsync(timeout.Token);
            var response = JsonSerializer.Deserialize<ElevatedResponse>(json, ElevatedResponse.JsonOptions);

            return response is { Success: true, Payload: not null }
                ? new ElevationResult(ElevationStatus.Success, response.Payload)
                : new ElevationResult(ElevationStatus.Failed);
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

    /// <summary>Läuft die App schon als Admin, braucht es keinen Hilfsprozess.</summary>
    private async Task<ElevationResult> RunInProcessAsync(string operationId, CancellationToken cancellationToken)
    {
        var operation = _operations.FirstOrDefault(op => op.Id == operationId);
        if (operation is null)
        {
            _logger.LogError("Unbekannte Aktion {OperationId}", operationId);
            return new ElevationResult(ElevationStatus.Failed);
        }

        return new ElevationResult(ElevationStatus.Success, await operation.RunAsync(cancellationToken));
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
