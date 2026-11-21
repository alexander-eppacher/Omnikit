using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

/// <summary>Alle Aufgaben: oben die, die auf Freigabe warten, darunter die letzten 100.</summary>
public sealed class TasksModel(ServerDbContext db, TaskService tasks) : ServerPage
{
    public IReadOnlyList<DeviceTask> Waiting { get; private set; } = [];

    public IReadOnlyList<DeviceTask> Recent { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = db.Tasks.Include(task => task.Device).AsNoTracking();
        Waiting = await query.Where(task => task.State == TaskState.AwaitingApproval).OrderBy(task => task.Id).ToListAsync(cancellationToken);
        Recent = await query.Where(task => task.State != TaskState.AwaitingApproval).OrderByDescending(task => task.Id).Take(100).ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostApproveAsync(int taskId, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await tasks.ApproveAsync(UserName, taskId, cancellationToken), ServerTexts.Task_Approved) : Forbid();

    public async Task<IActionResult> OnPostRejectAsync(int taskId, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await tasks.RejectAsync(UserName, taskId, cancellationToken), ServerTexts.Task_Rejected) : Forbid();
}
