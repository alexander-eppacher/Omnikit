using System.Globalization;
using Omnikit.Server.Data;

using Omnikit.Server.Resources;

namespace Omnikit.Server.Services;

/// <summary>
/// Schreibt Einträge ins Audit-Log. Der Eintrag wird mit dem nächsten <c>SaveChanges</c> gespeichert –
/// also zusammen mit der Änderung, die er beschreibt (entweder beides oder nichts).
/// </summary>
public sealed class AuditLog(ServerDbContext db, TimeProvider time)
{
    public void Add(string actor, string action, string details) =>
        db.Audit.Add(new AuditEntry { At = time.GetUtcNow().UtcDateTime, Actor = actor, Action = action, Details = details });

    /// <summary>So erscheint der Server selbst im Audit-Log (z. B. beim ersten Start oder wenn Aufgaben ablaufen).</summary>
    public const string SystemActor = "System";

    /// <summary>So erscheint ein Agent im Audit-Log.</summary>
    public static string DeviceActor(Device device) => string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_Device, device.Id, device.Name);
}
