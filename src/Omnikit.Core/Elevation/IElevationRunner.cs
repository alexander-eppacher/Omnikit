namespace Omnikit.Core.Elevation;

/// <summary>
/// Führt eine <see cref="IElevatedOperation"/> mit Adminrechten aus.
/// </summary>
public interface IElevationRunner
{
    /// <param name="arguments">Parameter für die Aktion (werden dort geprüft).</param>
    /// <param name="progress">Empfängt Fortschrittsmeldungen des Hilfsprozesses; kann null sein.</param>
    Task<ElevationResult> RunAsync(
        string operationId, IReadOnlyList<string> arguments, IProgress<string>? progress, CancellationToken cancellationToken);
}

public enum ElevationStatus
{
    /// <summary>Hat geklappt, <see cref="ElevationResult.Payload"/> enthält die Daten.</summary>
    Success,

    /// <summary>Der Nutzer hat die Windows-Abfrage abgelehnt oder kein Admin-Passwort eingegeben.</summary>
    Cancelled,

    /// <summary>Technischer Fehler oder Zeitüberschreitung (Details im Log).</summary>
    Failed,

    /// <summary>Auf diesem System nicht möglich (z. B. Linux).</summary>
    NotSupported,
}

/// <param name="Payload">Ergebnis der Aktion bei Erfolg; bei einem erwarteten Fehler der Aktion eine verständliche Meldung.</param>
public sealed record ElevationResult(ElevationStatus Status, string? Payload = null);
