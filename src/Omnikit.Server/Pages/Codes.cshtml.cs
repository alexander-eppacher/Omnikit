using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Services;

namespace Omnikit.Server.Pages;

/// <summary>Registrierungscodes (nur Admins, siehe Program.cs). Der Code wird nur direkt nach dem Erstellen angezeigt.</summary>
public sealed class CodesModel(ServerDbContext db, RegistrationService registration, TimeProvider time) : ServerPage
{
    public string? NewCode { get; private set; }

    public IReadOnlyList<DeviceGroup> Groups { get; private set; } = [];

    public IReadOnlyList<RegistrationCode> Open { get; private set; } = [];

    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    public async Task OnPostAsync(int? groupId, CancellationToken cancellationToken)
    {
        NewCode = await registration.CreateCodeAsync(UserName, groupId, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        Groups = await db.Groups.OrderBy(g => g.Name).AsNoTracking().ToListAsync(cancellationToken);
        Open = await db.RegistrationCodes.Where(code => code.UsedAt == null && code.ExpiresAt > now)
            .OrderByDescending(code => code.Id).AsNoTracking().ToListAsync(cancellationToken);
    }
}
