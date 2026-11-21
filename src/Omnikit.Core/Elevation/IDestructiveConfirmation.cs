namespace Omnikit.Core.Elevation;

/// <summary>
/// Letzte Rückfrage vor einer Aktion, die Daten unwiderruflich löscht – angezeigt vom Prozess, der die Aktion ausführt.
/// Im Hilfsprozess ist das ein eigenes Windows-Fenster mit Adminrechten: Ein Programm ohne Adminrechte kann es weder
/// fälschen noch anklicken. So kann auch Schadsoftware, die den Hilfsprozess selbst startet, nichts ohne sichtbare Rückfrage löschen.
/// </summary>
public interface IDestructiveConfirmation
{
    /// <param name="message">Was genau passiert – mit dem Ziel, wie es der ausführende Prozess selbst ermittelt hat.</param>
    /// <returns><c>true</c> nur, wenn der Nutzer ausdrücklich zugestimmt hat.</returns>
    bool Confirm(string title, string message);
}

/// <summary>
/// Für die App, wenn sie selbst schon mit Adminrechten läuft und die Aktion ohne Hilfsprozess ausführt: Dann hat der Nutzer
/// gerade im Fenster der App bestätigt, und kein Programm ohne Adminrechte kann sich dazwischenschalten.
/// </summary>
public sealed class ConfirmedInApp : IDestructiveConfirmation
{
    public bool Confirm(string title, string message) => true;
}
