namespace Omnikit.Core.Elevation;

/// <summary>
/// Eine fest eingebaute Aktion, die Adminrechte braucht (z. B. Laufwerksdetails lesen oder einen USB-Stick einrichten).
/// Nur Aktionen, die als <see cref="IElevatedOperation"/> registriert sind, kann der Hilfsprozess ausführen.
/// Das ist die "feste Liste erlaubter Aktionen": Beliebige Befehle sind nicht möglich.
/// </summary>
public interface IElevatedOperation
{
    /// <summary>Eindeutiger Name, z. B. "health.disk-reliability".</summary>
    string Id { get; }

    /// <summary>Wie lange die Aktion höchstens dauern darf. Leseaktionen: 30 Sekunden.</summary>
    TimeSpan Timeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Führt die Aktion aus und gibt das Ergebnis als Text (meist JSON) zurück.
    /// Leseaktionen verändern nichts. Ändernde Aktionen startet die App nur nach Bestätigung durch den Nutzer.
    /// </summary>
    /// <param name="arguments">
    /// Kommen über die Befehlszeile von außen. Die Aktion muss sie selbst streng prüfen und darf den Angaben der App
    /// nicht blind vertrauen (z. B. noch einmal prüfen, ob ein Datenträger wirklich ein USB-Stick ist).
    /// </param>
    /// <param name="progress">Kurze Fortschrittsmeldungen für die App, z. B. „Dateien werden kopiert …“.</param>
    Task<string> RunAsync(IReadOnlyList<string> arguments, IProgress<string> progress, CancellationToken cancellationToken);
}
