using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Security;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

/// <summary>Eigenes Passwort ändern.</summary>
public sealed class AccountModel(UserService users, ServerDbContext db) : ServerPage
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string current, string next, CancellationToken cancellationToken)
    {
        var result = await users.ChangePasswordAsync(UserName, current, next, cancellationToken);
        if (result.Success)
        {
            // Mit dem neuen Passwort gelten alle bisherigen Anmeldungen nicht mehr – diese hier wird erneuert, die anderen fliegen raus
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserName == UserName, cancellationToken);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionStamp.Principal(user));
        }

        return Done(result, ServerTexts.Account_Changed);
    }
}
