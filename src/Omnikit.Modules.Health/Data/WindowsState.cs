namespace Omnikit.Modules.Health.Data;

/// <summary>
/// Zustand von Windows. <c>null</c> heißt jeweils: konnte nicht ermittelt werden.
/// </summary>
public sealed record WindowsState(DateTimeOffset? LastBootTime, bool? RebootPending, bool? IsActivated);

/// <summary>
/// Ergebnis der Suche nach Windows-Updates.
/// </summary>
/// <param name="Total">Anzahl verfügbarer, noch nicht installierter Updates.</param>
/// <param name="Security">Davon Sicherheitsupdates (mit Microsoft-Schweregrad).</param>
public sealed record UpdateStatus(int Total, int Security);
