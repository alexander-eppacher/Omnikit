using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;

namespace Omnikit.Server.Services;

/// <summary>Benutzer der Verwaltung. Passwörter nur als PBKDF2-Hash (PasswordHasher von ASP.NET Core Identity).</summary>
public sealed class UserService(ServerDbContext db, AuditLog audit, TimeProvider time)
{
    public const string InitialAdminName = "admin";
    public const int MinPasswordLength = 12;

    public const int MaxUserNameLength = 32;

    private static readonly PasswordHasher<User> Hasher = new();

    /// <summary>Legt beim ersten Start den Admin an, wenn es noch gar keinen Benutzer gibt.</summary>
    /// <returns><c>false</c>, wenn kein Benutzer existiert und kein brauchbares Passwort gesetzt ist.</returns>
    public async Task<bool> EnsureInitialAdminAsync(string? password, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return true;
        }

        if (password is null || password.Length < MinPasswordLength)
        {
            return false;
        }

        Add(InitialAdminName, password, UserRole.Admin);
        audit.Add(AuditLog.SystemActor, "user.create", string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_FirstAdmin, InitialAdminName));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <returns>Der Benutzer bei richtigem Passwort, sonst <c>null</c>.</returns>
    public async Task<User?> VerifyAsync(string userName, string password, CancellationToken cancellationToken)
    {
        // Groß-/Kleinschreibung zählt nicht („Admin“ = „admin“); Namen sind nur ASCII, damit das in jeder Datenbank gleich gilt
        var lower = userName.ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(IsNamed(lower), cancellationToken);
        if (user is null)
        {
            // Trotzdem hashen, damit man an der Antwortzeit nicht erkennt, ob es den Namen gibt
            Hasher.HashPassword(new User { UserName = userName, PasswordHash = string.Empty }, password);
            return null;
        }

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<ServiceResult> CreateAsync(string by, string userName, string password, UserRole role, CancellationToken cancellationToken)
    {
        userName = userName?.Trim() ?? string.Empty;
        if (userName.Length is 0 or > MaxUserNameLength || !userName.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
        {
            return ServiceResult.Fail(ServerTexts.Error_UserName);
        }

        if ((password?.Length ?? 0) < MinPasswordLength)
        {
            return ServiceResult.Fail(ServerTexts.Error_PasswordShort);
        }

        if (!Enum.IsDefined(role))
        {
            // Ein Formular kann jede Zahl schicken – nur die Rollen annehmen, die es wirklich gibt
            return ServiceResult.Fail(ServerTexts.Error_Role);
        }

        var lower = userName.ToLowerInvariant();
        if (await db.Users.AnyAsync(IsNamed(lower), cancellationToken))
        {
            return ServiceResult.Fail(ServerTexts.Error_UserExists);
        }

        Add(userName, password!, role);
        audit.Add(by, "user.create", $"{userName} ({role})");
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    /// <summary>Sich selbst und den letzten Admin kann man nicht löschen (sonst käme niemand mehr hinein).</summary>
    public async Task<ServiceResult> DeleteAsync(string by, int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_NotFound);
        }

        if (user.UserName == by
            || (user.Role == UserRole.Admin && await db.Users.CountAsync(u => u.Role == UserRole.Admin, cancellationToken) <= 1))
        {
            return ServiceResult.Fail(ServerTexts.Error_CannotDeleteUser);
        }

        db.Users.Remove(user);
        audit.Add(by, "user.delete", user.UserName);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    public async Task<ServiceResult> ChangePasswordAsync(string userName, string current, string next, CancellationToken cancellationToken)
    {
        var user = await VerifyAsync(userName, current, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail(ServerTexts.Error_PasswordWrong);
        }

        if ((next?.Length ?? 0) < MinPasswordLength)
        {
            return ServiceResult.Fail(ServerTexts.Error_PasswordShort);
        }

        user.PasswordHash = Hasher.HashPassword(user, next!);
        audit.Add(userName, "user.password", ServerTexts.Audit_OwnPassword);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok;
    }

    /// <summary>Benutzer mit diesem Namen (in Kleinbuchstaben), ohne Rücksicht auf Groß-/Kleinschreibung.</summary>
    /// <remarks>Läuft als SQL („lower(...)“) in der Datenbank, nicht in .NET – deshalb ohne Kultur-Angabe.</remarks>
#pragma warning disable CA1304, CA1311, CA1862
    private static System.Linq.Expressions.Expression<Func<User, bool>> IsNamed(string lowerName) => user => user.UserName.ToLower() == lowerName;
#pragma warning restore CA1304, CA1311, CA1862

    private void Add(string userName, string password, UserRole role)
    {
        var user = new User { UserName = userName, PasswordHash = string.Empty, Role = role, CreatedAt = time.GetUtcNow().UtcDateTime };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.Users.Add(user);
    }
}
