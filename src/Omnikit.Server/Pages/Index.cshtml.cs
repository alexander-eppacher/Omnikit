using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Omnikit.Core.Modules;
using Omnikit.Server.Data;

namespace Omnikit.Server.Pages;

/// <summary>Startseite: alle Geräte mit Ampel.</summary>
public sealed class IndexModel(ServerDbContext db) : PageModel
{
    /// <summary>Eine Zeile der Liste – nur die angezeigten Werte, nicht der ganze Bericht und die Programmliste jedes Geräts.</summary>
    public sealed record DeviceRow(
        int Id, string Name, bool IsVirtualMachine, Severity? LastSeverity, string? GroupName, string? OsName, string? Model,
        DateTime? LastSeenAt, int OpenTasks);

    public IReadOnlyList<DeviceRow> Devices { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Devices = await db.Devices
            .OrderBy(device => device.Name)
            .Select(device => new DeviceRow(
                device.Id, device.Name, device.IsVirtualMachine, device.LastSeverity, device.Group == null ? null : device.Group.Name,
                device.OsName, device.Model, device.LastSeenAt,
                device.Tasks.Count(task => task.State == TaskState.AwaitingApproval || task.State == TaskState.Approved || task.State == TaskState.Running)))
            .ToListAsync(cancellationToken);
}
