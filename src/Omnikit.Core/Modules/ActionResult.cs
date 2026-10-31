namespace Omnikit.Core.Modules;

/// <summary>
/// Ergebnis einer ausgeführten <see cref="ModuleAction"/>.
/// </summary>
/// <param name="Success">Ob die Aktion geklappt hat.</param>
/// <param name="Message">Verständliche Meldung für den Nutzer. Technische Details gehören ins Log.</param>
public sealed record ActionResult(bool Success, string Message);
