using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;

namespace Omnikit.Server.Pages;

/// <summary>Die letzten 300 Einträge des Audit-Logs (nur Admins, siehe Program.cs).</summary>
public sealed class AuditModel(ServerDbContext db) : ServerPage
{
    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Entries = await db.Audit.OrderByDescending(entry => entry.Id).Take(300).AsNoTracking().ToListAsync(cancellationToken);
}
