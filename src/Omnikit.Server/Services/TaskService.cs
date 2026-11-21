using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Omnikit.Modules.Apps.Winget;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Shared;

namespace Omnikit.Server.Services;

/// <summary>
/// Aufgaben anlegen, freigeben, ablehnen. Eine Aufgabe läuft erst nach der Freigabe durch einen Admin
/// (die Rolle prüfen die Seiten; hier wird nur der Ablauf geprüft).
/// </summary>
public sealed class TaskService(ServerDbContext db, AuditLog audit, TimeProvider time)
{
    private static readonly TaskState[] OpenStates = [TaskState.AwaitingApproval, TaskState.Approved, TaskState.Running];

    public async Task<ServiceResult> CreateAsync(string user, int deviceId, AgentTaskKind kind, string packageId, CancellationToken cancellationToken)
    {
        packageId = packageId?.Trim() ?? string.Empty;
        if (!PackageId.IsValid(packageId) || !Enum.IsDefined(kind))
        {
            return ServiceResult.Fail(ServerTexts.Error_PackageId);
        }

        var device = await db.Devices.FindAsync([deviceId], cancellationToken);
        if (device is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        Add(user, device, kind, packageId);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    public Task<ServiceResult> ApproveAsync(string user, int taskId, CancellationToken cancellationToken) =>
        DecideAsync(user, taskId, TaskState.Approved, "task.approve", cancellationToken);

    public Task<ServiceResult> RejectAsync(string user, int taskId, CancellationToken cancellationToken) =>
        DecideAsync(user, taskId, TaskState.Rejected, "task.reject", cancellationToken);

    /// <summary>
    /// Legt für jedes Gerät der Gruppe eine Aufgabe für jedes fehlende Pflichtprogramm an (wartet dann auf Freigabe).
    /// Geräte ohne Programmliste werden übersprungen; für Programme mit offener Aufgabe entsteht keine zweite.
    /// </summary>
    /// <returns>Anzahl neuer Aufgaben.</returns>
    public async Task<int> CreateMissingForGroupAsync(string user, int groupId, CancellationToken cancellationToken)
    {
        var group = await db.Groups
            .Include(g => g.RequiredPackages)
            .Include(g => g.Devices).ThenInclude(device => device.Tasks)
            .SingleOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return 0;
        }

        var created = 0;
        foreach (var device in group.Devices.Where(device => device.InstalledJson is not null))
        {
            var installed = JsonSerializer.Deserialize<List<InstalledProgram>>(device.InstalledJson!, AgentApi.JsonOptions) ?? [];
            var installedIds = installed.Select(program => program.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var openIds = device.Tasks.Where(task => OpenStates.Contains(task.State)).Select(task => task.PackageId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var package in group.RequiredPackages.Where(p => !installedIds.Contains(p.PackageId) && !openIds.Contains(p.PackageId)))
            {
                Add(user, device, AgentTaskKind.Install, package.PackageId);
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    private void Add(string user, Device device, AgentTaskKind kind, string packageId)
    {
        db.Tasks.Add(new DeviceTask
        {
            DeviceId = device.Id,
            Kind = kind,
            PackageId = packageId,
            State = TaskState.AwaitingApproval,
            CreatedBy = user,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        });
        audit.Add(user, "task.create", string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_TaskCreate, kind, packageId, AuditLog.DeviceActor(device)));
    }

    private async Task<ServiceResult> DecideAsync(string user, int taskId, TaskState decision, string action, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.Include(t => t.Device).SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        if (task.State != TaskState.AwaitingApproval)
        {
            return ServiceResult.Fail(ServerTexts.Error_TaskAlreadyDecided);
        }

        task.State = decision;
        task.DecidedBy = user;
        task.DecidedAt = time.GetUtcNow().UtcDateTime;
        audit.Add(user, action, string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_TaskDecision, task.Id, task.Kind, task.PackageId, task.DeviceId, task.Device?.Name));
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }
}
