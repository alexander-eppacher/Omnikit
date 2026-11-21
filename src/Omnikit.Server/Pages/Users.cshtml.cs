using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

/// <summary>Benutzer verwalten (nur Admins, siehe Program.cs).</summary>
public sealed class UsersModel(ServerDbContext db, UserService users) : ServerPage
{
    public IReadOnlyList<User> Users { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Users = await db.Users.OrderBy(user => user.UserName).AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(string userName, string password, UserRole role, CancellationToken cancellationToken) =>
        Done(await users.CreateAsync(UserName, userName, password, role, cancellationToken), ServerTexts.Saved);

    public async Task<IActionResult> OnPostDeleteAsync(int userId, CancellationToken cancellationToken) =>
        Done(await users.DeleteAsync(UserName, userId, cancellationToken), ServerTexts.Saved);
}
