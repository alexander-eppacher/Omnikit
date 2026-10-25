using PcTool.Core.Modules;
using PcTool.Core.SystemInfo;

namespace PcTool.Core.Tests.Fakes;

/// <summary>
/// Einfaches Test-Modul: Ob es unterstützt wird, bestimmt der Test über eine Funktion.
/// </summary>
internal sealed class FakeModule : IModule
{
    private readonly Func<SystemContext, bool> _isSupported;

    public FakeModule(string id, Func<SystemContext, bool>? isSupported = null)
    {
        Id = id;
        _isSupported = isSupported ?? (_ => true);
    }

    public string Id { get; }
    public string DisplayName => Id;

    public bool IsSupported(SystemContext context) => _isSupported(context);

    public Task<ModuleReport> CheckAsync(SystemContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleReport(Id, []));

    public IReadOnlyList<ModuleAction> GetActions(ModuleReport report) => [];

    public Task<ActionResult> ExecuteAsync(ModuleAction action, CancellationToken cancellationToken) =>
        Task.FromResult(new ActionResult(true, "ok"));
}
