using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Security;
using Omnikit.Shared;

namespace Omnikit.Server.Services;

/// <summary>Einmal-Codes erstellen und gegen einen Geräte-Token tauschen.</summary>
public sealed class RegistrationService(ServerDbContext db, AuditLog audit, TimeProvider time)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromHours(24);

    /// <returns>Der Code im Klartext – wird nur dieses eine Mal angezeigt, gespeichert ist nur sein Hash.</returns>
    public async Task<string> CreateCodeAsync(string createdBy, int? groupId, CancellationToken cancellationToken)
    {
        var code = SecretTokens.NewCode();
        var now = time.GetUtcNow().UtcDateTime;
        db.RegistrationCodes.Add(new RegistrationCode
        {
            CodeHash = SecretTokens.Hash(SecretTokens.NormalizeCode(code)),
            CreatedAt = now,
            ExpiresAt = now + CodeLifetime,
            GroupId = groupId,
            CreatedBy = createdBy,
        });
        audit.Add(createdBy, "code.create", groupId is null ? ServerTexts.Audit_CodeWithoutGroup : string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_CodeForGroup, groupId));
        await db.SaveChangesAsync(cancellationToken);
        return code;
    }

    /// <returns><c>null</c>, wenn der Code unbekannt, schon benutzt oder abgelaufen ist (bewusst ohne Unterscheidung).</returns>
    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var hash = SecretTokens.Hash(SecretTokens.NormalizeCode(request.Code ?? string.Empty));
        var now = time.GetUtcNow().UtcDateTime;

        // Alles in einer Transaktion: Gerät, „Code benutzt“ und Audit-Eintrag entstehen gemeinsam oder gar nicht
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var code = await db.RegistrationCodes.SingleOrDefaultAsync(c => c.CodeHash == hash, cancellationToken);
        if (code is null || code.UsedAt is not null || code.ExpiresAt < now)
        {
            return null;
        }

        // Code für eine inzwischen gelöschte Gruppe: nicht mehr gültig – sonst landet das Gerät unbemerkt ohne Gruppe
        // (und ohne deren Pflichtprogramme)
        if (code.GroupId is int groupId && !await db.Groups.AnyAsync(group => group.Id == groupId, cancellationToken))
        {
            return null;
        }

        var token = SecretTokens.NewToken();
        var device = new Device
        {
            Name = ComputerNames.Clean(request.ComputerName),
            TokenHash = SecretTokens.Hash(token),
            RegisteredAt = now,
            GroupId = code.GroupId,
        };
        code.UsedAt = now;
        db.Devices.Add(device);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Eine zweite Anfrage hat denselben Code im selben Moment eingelöst (UsedAt ist ein Concurrency-Token):
            // Nur die erste bekommt ein Gerät, diese hier geht leer aus.
            return null;
        }

        // Die Geräte-Nummer für den Audit-Eintrag gibt es erst nach dem Speichern
        audit.Add(AuditLog.DeviceActor(device), "device.register", string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_CodeBy, code.CreatedBy));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegisterResponse(device.Id, token);
    }
}

/// <summary>Computernamen kommen vom Gerät und werden nur angezeigt – trotzdem begrenzen.</summary>
public static class ComputerNames
{
    public static string Clean(string? name)
    {
        var cleaned = new string((name ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        return cleaned.Length == 0 ? "?" : cleaned[..Math.Min(cleaned.Length, 64)];
    }
}
