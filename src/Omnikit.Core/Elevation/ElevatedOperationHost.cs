using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Omnikit.Core.Elevation;

/// <summary>
/// Läuft im Hilfsprozess (mit Adminrechten, ohne Fenster).
/// Erwartet die Argumente: --elevated-operation &lt;Aktions-Id&gt; &lt;Pipe-Name&gt; [Parameter …].
/// Verbindet sich mit der Named Pipe der App, schickt Fortschrittsmeldungen und zum Schluss das Ergebnis, dann beendet er sich.
/// Jede Nachricht ist eine Zeile JSON (<see cref="ElevatedMessage"/>).
/// </summary>
public static class ElevatedOperationHost
{
    public const string Argument = "--elevated-operation";

    private const string PipeNamePrefix = "Omnikit-";
    private const int MaxArguments = 8;
    private const int MaxArgumentLength = 1024;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    public static bool IsHelperMode(string[] args) => args.Length > 0 && args[0] == Argument;

    /// <summary>Zufälliger Pipe-Name, damit kein anderes Programm ihn vorher belegen kann.</summary>
    public static string CreatePipeName() => PipeNamePrefix + Guid.NewGuid().ToString("N");

    /// <returns>Exit-Code: 0 = Erfolg, sonst Fehler (Details im Log).</returns>
    public static async Task<int> RunAsync(string[] args, IEnumerable<IElevatedOperation> operations, ILogger logger)
    {
        if (!TryParseArguments(args, out var operationId, out var pipeName, out var arguments))
        {
            logger.LogError("Hilfsprozess: ungültige Argumente");
            return 2;
        }

        var operation = operations.FirstOrDefault(op => op.Id == operationId);
        if (operation is null)
        {
            // Nicht in der festen Liste: wird abgelehnt.
            logger.LogError("Hilfsprozess: unbekannte Aktion {OperationId}", operationId);
            return 3;
        }

        // „Identification“: Die Gegenseite der Pipe darf erfahren, wer sich verbindet, aber nicht mit dessen Adminrechten handeln
        await using var pipe = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous,
            OperatingSystem.IsWindows() ? TokenImpersonationLevel.Identification : TokenImpersonationLevel.None);
        await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };

        // Fortschritt sofort weiterreichen; Fehler beim Schreiben (App geschlossen) brechen die Aktion nicht ab.
        var progress = new SynchronousProgress(text => TrySend(writer, new ElevatedMessage(Progress: text), logger));

        var response = await RunOperationAsync(operation, arguments, progress, logger, CancellationToken.None);
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, ElevatedMessage.JsonOptions));

        return response.Success == true ? 0 : 4;
    }

    /// <summary>
    /// Führt die Aktion aus und macht aus jedem Ausgang eine Antwort – im Hilfsprozess und ebenso, wenn die App schon als Admin
    /// läuft (dann ohne Hilfsprozess, siehe <see cref="ElevationRunner"/>). Keine Ausnahme kommt heraus.
    /// </summary>
    internal static async Task<ElevatedMessage> RunOperationAsync(
        IElevatedOperation operation, IReadOnlyList<string> arguments, IProgress<string> progress, ILogger logger, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(operation.Timeout);
        try
        {
            var payload = await operation.RunAsync(arguments, progress, timeout.Token);
            logger.LogInformation("Hilfsprozess: Aktion {OperationId} erfolgreich", operation.Id);
            return new ElevatedMessage(Success: true, Payload: payload);
        }
        catch (ElevatedOperationException ex)
        {
            // Erwarteter Fehler mit verständlicher Meldung (z. B. „Das ist kein USB-Stick“)
            logger.LogWarning(ex, "Hilfsprozess: Aktion {OperationId} abgelehnt", operation.Id);
            return new ElevatedMessage(Success: false, Payload: ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Aktion {OperationId} abgebrochen", operation.Id);
            return new ElevatedMessage(Success: false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            logger.LogError("Zeitüberschreitung bei Aktion {OperationId} (mehr als {Timeout})", operation.Id, operation.Timeout);
            return new ElevatedMessage(Success: false);
        }
#pragma warning disable CA1031 // Jeder Fehler soll als "fehlgeschlagen" an die App zurückgemeldet werden, statt den Hilfsprozess abstürzen zu lassen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Hilfsprozess: Aktion {OperationId} fehlgeschlagen", operation.Id);
            return new ElevatedMessage(Success: false);
        }
    }

    private static void TrySend(StreamWriter writer, ElevatedMessage message, ILogger logger)
    {
        try
        {
            writer.WriteLine(JsonSerializer.Serialize(message, ElevatedMessage.JsonOptions));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Hilfsprozess: Fortschritt konnte nicht gesendet werden");
        }
    }

    /// <summary>
    /// Prüft die Argumente streng: Schalter, Aktions-Id, Pipe-Name im erwarteten Format,
    /// danach höchstens 8 Parameter ohne Steuerzeichen und mit begrenzter Länge.
    /// </summary>
    internal static bool TryParseArguments(string[] args, out string operationId, out string pipeName, out IReadOnlyList<string> arguments)
    {
        operationId = string.Empty;
        pipeName = string.Empty;
        arguments = [];

        if (args.Length < 3 || args.Length > 3 + MaxArguments || args[0] != Argument || string.IsNullOrWhiteSpace(args[1]))
        {
            return false;
        }

        var name = args[2];
        if (!name.StartsWith(PipeNamePrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(name[PipeNamePrefix.Length..], "N", out _))
        {
            return false;
        }

        var rest = args[3..];
        if (rest.Any(argument => argument.Length > MaxArgumentLength || argument.Any(char.IsControl)))
        {
            return false;
        }

        operationId = args[1];
        pipeName = name;
        arguments = rest;
        return true;
    }

    /// <summary>Wie <see cref="Progress{T}"/>, aber ohne Umweg über den Thread-Pool: Meldungen kommen in der richtigen Reihenfolge an.</summary>
    private sealed class SynchronousProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}

/// <summary>Eine Zeile über die Pipe: entweder Fortschritt oder (zum Schluss) das Ergebnis.</summary>
internal sealed record ElevatedMessage(string? Progress = null, bool? Success = null, string? Payload = null)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

/// <summary>Erwarteter Fehler einer Aktion; die Meldung ist für den Nutzer gedacht und wird an die App zurückgegeben.</summary>
public sealed class ElevatedOperationException : Exception
{
    public ElevatedOperationException()
    {
    }

    public ElevatedOperationException(string message)
        : base(message)
    {
    }

    public ElevatedOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
