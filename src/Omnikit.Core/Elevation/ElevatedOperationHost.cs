using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Omnikit.Core.Elevation;

/// <summary>
/// Läuft im Hilfsprozess (mit Adminrechten, ohne Fenster).
/// Erwartet genau die Argumente: --elevated-operation &lt;Aktions-Id&gt; &lt;Pipe-Name&gt;.
/// Führt die Aktion aus, schickt das Ergebnis über die Named Pipe an die App zurück und beendet sich.
/// </summary>
public static class ElevatedOperationHost
{
    public const string Argument = "--elevated-operation";

    private const string PipeNamePrefix = "Omnikit-";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);

    public static bool IsHelperMode(string[] args) => args.Length > 0 && args[0] == Argument;

    /// <summary>Zufälliger Pipe-Name, damit kein anderes Programm ihn vorher belegen kann.</summary>
    public static string CreatePipeName() => PipeNamePrefix + Guid.NewGuid().ToString("N");

    /// <returns>Exit-Code: 0 = Erfolg, sonst Fehler (Details im Log).</returns>
    public static async Task<int> RunAsync(string[] args, IEnumerable<IElevatedOperation> operations, ILogger logger)
    {
        if (!TryParseArguments(args, out var operationId, out var pipeName))
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

        var response = await RunOperationAsync(operation, logger);

        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync((int)ConnectTimeout.TotalMilliseconds);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(JsonSerializer.Serialize(response, ElevatedResponse.JsonOptions));

        return response.Success ? 0 : 4;
    }

    private static async Task<ElevatedResponse> RunOperationAsync(IElevatedOperation operation, ILogger logger)
    {
        using var timeout = new CancellationTokenSource(OperationTimeout);
        try
        {
            var payload = await operation.RunAsync(timeout.Token);
            logger.LogInformation("Hilfsprozess: Aktion {OperationId} erfolgreich", operation.Id);
            return new ElevatedResponse(true, payload);
        }
#pragma warning disable CA1031 // Jeder Fehler soll als "fehlgeschlagen" an die App zurückgemeldet werden, statt den Hilfsprozess abstürzen zu lassen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Hilfsprozess: Aktion {OperationId} fehlgeschlagen", operation.Id);
            return new ElevatedResponse(false, null);
        }
    }

    /// <summary>Prüft die Argumente streng: genau drei Teile, Pipe-Name im erwarteten Format.</summary>
    internal static bool TryParseArguments(string[] args, out string operationId, out string pipeName)
    {
        operationId = string.Empty;
        pipeName = string.Empty;

        if (args.Length != 3 || args[0] != Argument || string.IsNullOrWhiteSpace(args[1]))
        {
            return false;
        }

        var name = args[2];
        if (!name.StartsWith(PipeNamePrefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(name[PipeNamePrefix.Length..], "N", out _))
        {
            return false;
        }

        operationId = args[1];
        pipeName = name;
        return true;
    }
}

/// <summary>Was der Hilfsprozess über die Pipe zurückschickt.</summary>
internal sealed record ElevatedResponse(bool Success, string? Payload)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
