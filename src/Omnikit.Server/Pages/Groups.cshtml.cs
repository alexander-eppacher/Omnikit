using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

/// <summary>Gruppen mit Pflichtprogrammen. Ändern nur für Admins.</summary>
public sealed class GroupsModel(ServerDbContext db, GroupService groups, TaskService tasks) : ServerPage
{
    public IReadOnlyList<DeviceGroup> Groups { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Groups = await db.Groups.Include(g => g.RequiredPackages).Include(g => g.Devices)
            .OrderBy(g => g.Name).AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(string name, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await groups.CreateAsync(UserName, name, cancellationToken), ServerTexts.Saved) : Forbid();

    public async Task<IActionResult> OnPostDeleteAsync(int groupId, bool confirmed, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        return confirmed
            ? Done(await groups.DeleteAsync(UserName, groupId, cancellationToken), ServerTexts.Saved)
            : Done(ServiceResult.Fail(ServerTexts.Error_NotConfirmed), string.Empty);
    }

    public async Task<IActionResult> OnPostAddPackageAsync(int groupId, string? catalogId, string? packageId, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        var package = string.IsNullOrWhiteSpace(packageId) ? catalogId : packageId;
        return Done(await groups.AddPackageAsync(UserName, groupId, package ?? string.Empty, cancellationToken), ServerTexts.Saved);
    }

    public async Task<IActionResult> OnPostRemovePackageAsync(int entryId, CancellationToken cancellationToken) =>
        IsAdmin ? Done(await groups.RemovePackageAsync(UserName, entryId, cancellationToken), ServerTexts.Saved) : Forbid();

    public async Task<IActionResult> OnPostCreateTasksAsync(int groupId, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        var count = await tasks.CreateMissingForGroupAsync(UserName, groupId, cancellationToken);
        return Done(ServiceResult.Ok, string.Format(CultureInfo.CurrentCulture, ServerTexts.Groups_TasksCreated, count));
    }
}
