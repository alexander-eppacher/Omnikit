using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnikit.Core.Reports;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Services;
using Omnikit.Shared;

namespace Omnikit.Server.Pages;

/// <summary>Ein Gerät: letzter Bericht, Programme, Aufgaben. Ändern nur für Admins (in jedem Handler geprüft).</summary>
public sealed class DeviceModel(ServerDbContext db, TaskService tasks, GroupService groups) : ServerPage
{
    public Device? Device { get; private set; }

    public SystemReport? Report { get; private set; }

    public IReadOnlyList<InstalledProgram>? Installed { get; private set; }

    public IReadOnlyList<DeviceGroup> Groups { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        Device = await db.Devices.Include(d => d.Group).Include(d => d.Tasks)
            .AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (Device is null)
        {
            return NotFound();
        }

        Report = Display.Report(Device);
        Installed = Display.Installed(Device);
        Groups = await db.Groups.OrderBy(g => g.Name).AsNoTracking().ToListAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostTaskAsync(int id, AgentTaskKind kind, string? catalogId, string? packageId, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        var package = string.IsNullOrWhiteSpace(packageId) ? catalogId : packageId;
        return Done(await tasks.CreateAsync(UserName, id, kind, package ?? string.Empty, cancellationToken), ServerTexts.Task_Created, new { id });
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, int taskId, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await tasks.ApproveAsync(UserName, taskId, cancellationToken), ServerTexts.Task_Approved, new { id }) : Forbid();

    public async Task<IActionResult> OnPostRejectAsync(int id, int taskId, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await tasks.RejectAsync(UserName, taskId, cancellationToken), ServerTexts.Task_Rejected, new { id }) : Forbid();

    public async Task<IActionResult> OnPostGroupAsync(int id, int? groupId, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await groups.SetDeviceGroupAsync(UserName, id, groupId, cancellationToken), ServerTexts.Saved, new { id }) : Forbid();

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirmed, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        if (!confirmed)
        {
            return Done(ServiceResult.Fail(ServerTexts.Error_NotConfirmed), string.Empty, new { id });
        }

        var result = await groups.DeleteDeviceAsync(UserName, id, cancellationToken);
        TempData[result.Success ? "Message" : "Error"] = result.Success ? ServerTexts.Device_Removed : result.Error;
        return RedirectToPage("/Index");
    }
}
