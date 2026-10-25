namespace PcTool.Core.Elevation;

/// <summary>
/// Führt eine <see cref="IElevatedOperation"/> mit Adminrechten aus.
/// </summary>
public interface IElevationRunner
{
    Task<ElevationResult> RunAsync(string operationId, CancellationToken cancellationToken);
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

public sealed record ElevationResult(ElevationStatus Status, string? Payload = null);
