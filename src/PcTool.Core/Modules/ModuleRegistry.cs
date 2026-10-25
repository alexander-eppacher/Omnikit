using PcTool.Core.SystemInfo;

namespace PcTool.Core.Modules;

/// <summary>
/// Kennt alle registrierten Module und liefert die, die auf dem aktuellen Gerät unterstützt werden.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<IModule> _modules;

    public ModuleRegistry(IEnumerable<IModule> modules)
    {
        _modules = modules.ToList();

        var duplicateId = _modules
            .GroupBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateId is not null)
        {
            throw new ArgumentException($"Die Modul-Id '{duplicateId}' ist mehrfach registriert.", nameof(modules));
        }
    }

    /// <summary>Alle Module in Registrierungsreihenfolge.</summary>
    public IReadOnlyList<IModule> All => _modules;

    /// <summary>Nur die Module, die auf diesem Gerät sinnvoll sind.</summary>
    public IReadOnlyList<IModule> GetSupported(SystemContext context) =>
        _modules.Where(module => module.IsSupported(context)).ToList();
}
