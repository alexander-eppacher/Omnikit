using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Omnikit.Server.Data;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

/// <summary>Gemeinsames für alle Verwaltungsseiten: wer angemeldet ist, ob Admin, Rückmeldung nach einer Aktion.</summary>
public abstract class ServerPage : PageModel
{
    public bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    protected string UserName => User.Identity?.Name ?? "?";

    /// <summary>Zeigt Erfolg oder Fehler auf der nächsten Seite an und lädt die aktuelle Seite neu (Post-Redirect-Get).</summary>
    protected IActionResult Done(ServiceResult result, string successMessage, object? routeValues = null)
    {
        TempData[result.Success ? "Message" : "Error"] = result.Success ? successMessage : result.Error;
        return RedirectToPage(routeValues);
    }
}
