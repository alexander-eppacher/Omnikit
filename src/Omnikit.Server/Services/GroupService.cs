using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Omnikit.Modules.Apps.Winget;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;

namespace Omnikit.Server.Services;

/// <summary>Gruppen (z. B. „Computerraum 1“) mit Pflichtprogrammen, Geräte zuordnen und entfernen.</summary>
public sealed class GroupService(ServerDbContext db, AuditLog audit)
{
    public async Task<ServiceResult> CreateAsync(string user, string name, CancellationToken cancellationToken)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 64)
        {
            return ServiceResult.Fail(ServerTexts.Error_GroupName);
        }

        if (await db.Groups.AnyAsync(group => group.Name == name, cancellationToken))
        {
            return ServiceResult.Fail(ServerTexts.Error_GroupExists);
        }

        db.Groups.Add(new DeviceGroup { Name = name });
        audit.Add(user, "group.create", name);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    public async Task<ServiceResult> DeleteAsync(string user, int groupId, CancellationToken cancellationToken)
    {
        var group = await db.Groups.Include(g => g.RequiredPackages).SingleOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        db.GroupPackages.RemoveRange(group.RequiredPackages);
        db.Groups.Remove(group);
        audit.Add(user, "group.delete", group.Name);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    public async Task<ServiceResult> AddPackageAsync(string user, int groupId, string packageId, CancellationToken cancellationToken)
    {
        packageId = packageId?.Trim() ?? string.Empty;
        if (!PackageId.IsValid(packageId))
        {
            return ServiceResult.Fail(ServerTexts.Error_PackageId);
        }

        var group = await db.Groups.Include(g => g.RequiredPackages).SingleOrDefaultAsync(g => g.Id == groupId, cancellationToken);
        if (group is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        if (!group.RequiredPackages.Any(p => string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase)))
        {
            group.RequiredPackages.Add(new GroupPackage { PackageId = packageId });
            audit.Add(user, "group.package.add", $"{group.Name}: {packageId}");
            await db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult.Ok;
    }

    public async Task<ServiceResult> RemovePackageAsync(string user, int packageEntryId, CancellationToken cancellationToken)
    {
        var entry = await db.GroupPackages.FindAsync([packageEntryId], cancellationToken);
        if (entry is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        db.GroupPackages.Remove(entry);
        audit.Add(user, "group.package.remove", string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_GroupPackage, entry.GroupId, entry.PackageId));
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    /// <param name="groupId"><c>null</c> = ohne Gruppe.</param>
    public async Task<ServiceResult> SetDeviceGroupAsync(string user, int deviceId, int? groupId, CancellationToken cancellationToken)
    {
        var device = await db.Devices.FindAsync([deviceId], cancellationToken);
        if (device is null || (groupId is int id && await db.Groups.FindAsync([id], cancellationToken) is null))
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        device.GroupId = groupId;
        audit.Add(user, "device.group", string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_DeviceToGroup, AuditLog.DeviceActor(device), groupId?.ToString(CultureInfo.InvariantCulture) ?? ServerTexts.Audit_NoGroup));
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    /// <summary>Gerät entfernen: Sein Token gilt danach nicht mehr; der Agent muss neu registriert werden.</summary>
    public async Task<ServiceResult> DeleteDeviceAsync(string user, int deviceId, CancellationToken cancellationToken)
    {
        var device = await db.Devices.FindAsync([deviceId], cancellationToken);
        if (device is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        db.Devices.Remove(device);
        audit.Add(user, "device.delete", AuditLog.DeviceActor(device));
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }
}
