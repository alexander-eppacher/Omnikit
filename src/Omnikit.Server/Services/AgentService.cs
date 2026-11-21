using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Omnikit.Core.Modules;
using Omnikit.Core.Reports;
using Omnikit.Core.SystemInfo;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Security;
using Omnikit.Shared;

namespace Omnikit.Server.Services;

/// <summary>Was ein angemeldeter Agent tun darf: Bericht abgeben, freigegebene Aufgaben abholen, Ergebnis melden.</summary>
public sealed class AgentService(ServerDbContext db, AuditLog audit, TimeProvider time)
{
    // Mehr Programme hat kein normaler PC; schützt die Datenbank vor übergroßen Listen
    private const int MaxInstalled = 2000;
    private const int MaxMessageLength = 500;

    /// <summary>
    /// So lange darf ein Ergebnis ausbleiben, bevor die Aufgabe als fehlgeschlagen gilt.
    /// winget bricht eine einzelne Installation nach 30 Minuten ab; dazu etwas Luft.
    /// </summary>
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(45);

    /// <returns>Das Gerät zum Token oder <c>null</c> (unbekannter oder widerrufener Token).</returns>
    public async Task<Device?> FindDeviceAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var hash = SecretTokens.Hash(token);
        return await db.Devices.SingleOrDefaultAsync(device => device.TokenHash == hash, cancellationToken);
    }

    public async Task SaveReportAsync(Device device, ReportRequest request, CancellationToken cancellationToken)
    {
        var report = request.Report;
        device.Name = ComputerNames.Clean(request.ComputerName);
        device.LastSeenAt = time.GetUtcNow().UtcDateTime;
        device.OsName = Limit(report.System?.OsName);
        device.Model = Limit(report.System?.Model);
        device.IsVirtualMachine = report.System?.VmType is { } vm && vm != VmType.None;
        device.LastSeverity = report.Modules?.Count > 0 ? report.Modules.Max(module => module.OverallSeverity) : Severity.Ok;
        device.LastReportJson = JsonSerializer.Serialize(report, ReportExporter.JsonOptions);
        device.InstalledJson = request.Installed is null
            ? null
            : JsonSerializer.Serialize(request.Installed.Take(MaxInstalled).Where(program => program.Id is not null).ToList(), AgentApi.JsonOptions);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gibt die nächste freigegebene Aufgabe heraus (höchstens eine) und markiert sie als „läuft“ (jede Aufgabe genau einmal).
    /// Der Agent meldet das Ergebnis und fragt dann sofort nach der nächsten. So beginnt die Zeitgrenze jeder Aufgabe erst,
    /// wenn sie wirklich startet – bei mehreren Aufgaben auf einmal liefe sie für die letzten schon während der ersten.
    /// </summary>
    public async Task<IReadOnlyList<AgentTask>> TakeTasksAsync(Device device, CancellationToken cancellationToken)
    {
        await ExpireLostTasksAsync(device.Id, cancellationToken);
        var task = await db.Tasks
            .Where(task => task.DeviceId == device.Id && task.State == TaskState.Approved)
            .OrderBy(task => task.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        if (task is not null)
        {
            task.State = TaskState.Running;
            task.StartedAt = now;
        }

        device.LastSeenAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return task is null ? [] : [new AgentTask(task.Id, task.Kind, task.PackageId)];
    }

    /// <summary>
    /// Aufgaben, die länger als <see cref="AnswerTimeout"/> laufen, ohne dass ein Ergebnis kam (PC ausgeschaltet, Dienst neu gestartet,
    /// Netz weg), gelten als fehlgeschlagen – sonst blieben sie für immer „läuft“. Läuft beim Abruf eines Geräts und regelmäßig
    /// für alle Geräte (<see cref="LostTaskSweeper"/>), damit auch Geräte erfasst werden, die sich nie mehr melden.
    /// </summary>
    /// <param name="deviceId">Nur dieses Gerät; <c>null</c> = alle.</param>
    /// <returns>Wie viele Aufgaben als fehlgeschlagen markiert wurden.</returns>
    public async Task<int> ExpireLostTasksAsync(int? deviceId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var limit = now - AnswerTimeout;
        var lost = await db.Tasks
            .Include(task => task.Device)
            .Where(task => task.State == TaskState.Running && task.StartedAt < limit && (deviceId == null || task.DeviceId == deviceId))
            .ToListAsync(cancellationToken);
        foreach (var task in lost)
        {
            task.State = TaskState.Failed;
            task.FinishedAt = now;
            task.ResultMessage = ServerTexts.Task_NoAnswer;
            audit.Add(task.Device is { } device ? AuditLog.DeviceActor(device) : AuditLog.SystemActor, "task.result",
                string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_TaskNoAnswer, task.Id, task.Kind, task.PackageId));
        }

        if (lost.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return lost.Count;
    }

    /// <returns><c>false</c>, wenn die Aufgabe nicht zu diesem Gerät gehört oder nicht läuft.</returns>
    public async Task<bool> SaveResultAsync(Device device, int taskId, TaskResultRequest result, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(
            task => task.Id == taskId && task.DeviceId == device.Id && task.State == TaskState.Running, cancellationToken);
        if (task is null)
        {
            return false;
        }

        task.State = result.Success ? TaskState.Succeeded : TaskState.Failed;
        task.FinishedAt = time.GetUtcNow().UtcDateTime;
        task.ExitCode = result.ExitCode;
        task.ResultMessage = Limit(result.Message, MaxMessageLength);
        audit.Add(AuditLog.DeviceActor(device), "task.result", string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_TaskResult, task.Id, task.Kind, task.PackageId, task.State));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? Limit(string? text, int length = 200) => text is null ? null : text[..Math.Min(text.Length, length)];
}
