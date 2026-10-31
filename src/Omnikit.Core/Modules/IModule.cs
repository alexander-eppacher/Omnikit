using Omnikit.Core.SystemInfo;

namespace Omnikit.Core.Modules;

/// <summary>
/// Gemeinsame Schnittstelle aller Module (Health, Apps, ...).
/// Neue Module dürfen den Kern nicht verändern müssen.
/// </summary>
public interface IModule
{
    /// <summary>Technische Kennung, z. B. "health".</summary>
    string Id { get; }

    /// <summary>Name in der Navigationsleiste.</summary>
    string DisplayName { get; }

    /// <summary>Ob das Modul auf diesem Gerät sinnvoll ist (z. B. Akku-Prüfung in einer VM = false).</summary>
    bool IsSupported(SystemContext context);

    /// <summary>Prüft den Zustand. Darf das System NIEMALS verändern.</summary>
    Task<ModuleReport> CheckAsync(SystemContext context, CancellationToken cancellationToken);

    /// <summary>Mögliche Aktionen auf Grundlage eines Berichts.</summary>
    IReadOnlyList<ModuleAction> GetActions(ModuleReport report);

    /// <summary>Führt eine Aktion aus. Nur nach ausdrücklicher Bestätigung durch den Nutzer aufrufen.</summary>
    Task<ActionResult> ExecuteAsync(ModuleAction action, CancellationToken cancellationToken);
}
