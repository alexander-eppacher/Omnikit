namespace PcTool.Modules.Health.Data;

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

/// <summary>
/// Momentaufnahme der Auslastung.
/// </summary>
/// <param name="MemoryTotalBytes">Für Windows nutzbarer Arbeitsspeicher.</param>
/// <param name="MemoryFreeBytes">Davon verfügbar (frei oder schnell freigebbar), wie "Verfügbar" im Task-Manager.</param>
/// <param name="MemoryInstalledBytes">Eingebauter Arbeitsspeicher; oft mehr als nutzbar, weil die Grafik einen Teil reserviert.</param>
public sealed record SystemLoad(int? CpuPercent, long? MemoryTotalBytes, long? MemoryFreeBytes, long? MemoryInstalledBytes = null);
