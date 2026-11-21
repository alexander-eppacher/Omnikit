using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;

namespace Omnikit.Server.Services;

/// <summary>
/// Schreibt Einträge ins Audit-Log. Der Eintrag wird mit dem nächsten <c>SaveChanges</c> gespeichert –
/// also zusammen mit der Änderung, die er beschreibt (entweder beides oder nichts).
/// Gespeichert werden nur Ereignis-Code und Werte, nie ein fertiger Satz: Der Text entsteht erst bei der Anzeige
/// (<see cref="AuditText"/>), damit das Log später auch in anderen Sprachen lesbar ist.
/// </summary>
public sealed class AuditLog(ServerDbContext db, TimeProvider time)
{
    public void Add(AuditActor actor, string action, params (string Key, object? Value)[] values) =>
        db.Audit.Add(new AuditEntry
        {
            At = time.GetUtcNow().UtcDateTime,
            ActorKind = actor.Kind,
            ActorName = actor.Name,
            ActorDeviceId = actor.DeviceId,
            Action = action,
            Values = AuditText.Serialize(values),
        });
}

/// <summary>Wer etwas getan hat: ein Benutzer, ein Gerät (Agent), der Server selbst oder jemand Unbekanntes.</summary>
public readonly record struct AuditActor(AuditActorKind Kind, string Name, int? DeviceId = null)
{
    public static AuditActor User(string userName) => new(AuditActorKind.User, userName);

    public static AuditActor Device(Device device) => new(AuditActorKind.Device, device.Name, device.Id);

    /// <summary>Der Server selbst, z. B. beim ersten Start oder wenn Aufgaben ablaufen.</summary>
    public static AuditActor System { get; } = new(AuditActorKind.System, string.Empty);

    /// <summary>Nicht angemeldet, z. B. bei einer fehlgeschlagenen Anmeldung.</summary>
    public static AuditActor Anonymous { get; } = new(AuditActorKind.Anonymous, string.Empty);
}

/// <summary>
/// Alle Ereignis-Codes des Audit-Logs. Zu jedem gibt es in <c>ServerTexts.resx</c> einen Text
/// <c>AuditEvent_&lt;Code mit _ statt .&gt;</c> mit Platzhaltern wie <c>{package}</c> (ein Test prüft das).
/// Codes werden nie umbenannt: Sie stehen in den Datenbanken der Betreiber.
/// </summary>
public static class AuditEvents
{
    public const string UserCreate = "user.create";
    public const string UserDelete = "user.delete";
    public const string UserPassword = "user.password";
    public const string UserLogin = "user.login";
    public const string UserLoginFailed = "user.login.failed";
    public const string CodeCreate = "code.create";
    public const string DeviceRegister = "device.register";
    public const string DeviceGroup = "device.group";
    public const string DeviceDelete = "device.delete";
    public const string GroupCreate = "group.create";
    public const string GroupDelete = "group.delete";
    public const string GroupPackageAdd = "group.package.add";
    public const string GroupPackageRemove = "group.package.remove";
    public const string TaskCreate = "task.create";
    public const string TaskApprove = "task.approve";
    public const string TaskReject = "task.reject";
    public const string TaskResult = "task.result";

    /// <summary>Das Gerät hat sich zu einer laufenden Aufgabe nicht mehr gemeldet.</summary>
    public const string TaskTimeout = "task.timeout";

    public static IReadOnlyList<string> All { get; } =
    [
        UserCreate, UserDelete, UserPassword, UserLogin, UserLoginFailed, CodeCreate, DeviceRegister, DeviceGroup, DeviceDelete,
        GroupCreate, GroupDelete, GroupPackageAdd, GroupPackageRemove, TaskCreate, TaskApprove, TaskReject, TaskResult, TaskTimeout,
    ];

    /// <summary>Wert für „keine Gruppe“ (sprachneutral gespeichert).</summary>
    public const string None = "–";
}

/// <summary>Macht aus Ereignis-Code und Werten einen lesbaren Satz (erst bei der Anzeige).</summary>
public static partial class AuditText
{
    public static string Serialize((string Key, object? Value)[] values) =>
        JsonSerializer.Serialize(values.ToDictionary(pair => pair.Key, pair => Format(pair.Value)));

    public static IReadOnlyDictionary<string, string> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Der Text zum Ereignis mit eingesetzten Werten; unbekannte Codes erscheinen roh mit allen Werten.</summary>
    public static string Describe(AuditEntry entry)
    {
        var values = Deserialize(entry.Values);
        var template = ServerTexts.ResourceManager.GetString(TextKey(entry.Action), CultureInfo.CurrentUICulture);
        if (template is null)
        {
            return string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}").Prepend(entry.Action));
        }

        return Placeholder().Replace(template, match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : "?");
    }

    public static string Actor(AuditEntry entry) => entry.ActorKind switch
    {
        AuditActorKind.User => entry.ActorName,
        AuditActorKind.Device => string.Format(CultureInfo.CurrentCulture, ServerTexts.Audit_Device, entry.ActorDeviceId, entry.ActorName),
        AuditActorKind.System => ServerTexts.Audit_System,
        _ => ServerTexts.Audit_Unknown,
    };

    public static string TextKey(string action) => "AuditEvent_" + action.Replace('.', '_');

    /// <summary>Werte sprachneutral: Zahlen ohne Tausenderpunkt, Aufzählungen mit ihrem Namen.</summary>
    private static string Format(object? value) => value switch
    {
        null => AuditEvents.None,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
