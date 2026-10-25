namespace PcTool.Core.Elevation;

/// <summary>
/// Eine fest eingebaute Leseaktion, die Adminrechte braucht (z. B. Laufwerksdetails lesen).
/// Nur Aktionen, die als <see cref="IElevatedOperation"/> registriert sind, kann der Hilfsprozess ausführen.
/// Das ist die "feste Liste erlaubter Aktionen": Beliebige Befehle sind nicht möglich.
/// </summary>
public interface IElevatedOperation
{
    /// <summary>Eindeutiger Name, z. B. "health.disk-reliability".</summary>
    string Id { get; }

    /// <summary>Liest Daten und gibt sie als JSON-Text zurück. Darf nichts am System verändern.</summary>
    Task<string> RunAsync(CancellationToken cancellationToken);
}
