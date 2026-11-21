using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Security;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

public sealed class LoginModel(UserService users, AuditLog audit, ServerDbContext db, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken cancellationToken)
    {
        var user = await users.VerifyAsync(UserName.Trim(), Password, cancellationToken);
        if (user is null)
        {
            // Bewusst ohne Hinweis, ob der Name oder das Passwort falsch ist. Ins Audit-Log mit gekürztem Namen;
            // wie oft das geht, begrenzt die Anmeldebremse (20 je Minute und Adresse)
            var address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
            logger.LogWarning("Anmeldung fehlgeschlagen von {Address}", address);
            var attempted = UserName.Trim();
            audit.Add(ServerTexts.Audit_Unknown, "user.login.failed", string.Format(
                CultureInfo.CurrentCulture, ServerTexts.Audit_LoginFailed, attempted[..Math.Min(attempted.Length, UserService.MaxUserNameLength)], address));
            await db.SaveChangesAsync(cancellationToken);
            Error = ServerTexts.Login_Failed;
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionStamp.Principal(user));
        audit.Add(user.UserName, "user.login", ServerTexts.Audit_LoggedIn);
        await db.SaveChangesAsync(cancellationToken);

        // Nur auf Seiten dieses Servers zurückleiten (keine fremden Adressen)
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
