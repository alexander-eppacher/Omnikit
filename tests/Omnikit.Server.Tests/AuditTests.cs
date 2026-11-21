using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Resources;
using Omnikit.Server.Services;
using Omnikit.Shared;

using static Omnikit.Server.Tests.TestCancellation;

namespace Omnikit.Server.Tests;

public sealed partial class AuditTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void EveryEventHasAText()
    {
        foreach (var action in AuditEvents.All)
        {
            Assert.False(string.IsNullOrEmpty(ServerTexts.ResourceManager.GetString(AuditText.TextKey(action), CultureInfo.InvariantCulture)), action);
        }
    }

    [Fact]
    public void EventCodesAreUnique() => Assert.Equal(AuditEvents.All.Count, AuditEvents.All.Distinct(StringComparer.Ordinal).Count());

    [Fact]
    public async Task OnlyCodeAndValuesAreStoredAndTheTextComesLater()
    {
        var (db, audit) = _database.Open();
        await using (db)
        {
            var device = new Device { Id = 12, Name = "PC-07", TokenHash = "x", RegisteredAt = _database.Time.Now.UtcDateTime };
            audit.Add(AuditActor.Device(device), AuditEvents.TaskResult,
                ("task", 1234), ("kind", AgentTaskKind.Install), ("package", "Mozilla.Firefox"), ("state", TaskState.Succeeded), ("exitCode", 0));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = _database.CreateContext();
        var entry = await read.Audit.SingleAsync(Ct);

        // Gespeichert: sprachneutral, ohne Tausenderpunkt und ohne deutschen Satz
        Assert.Equal(AuditActorKind.Device, entry.ActorKind);
        Assert.Equal(12, entry.ActorDeviceId);
        Assert.Equal("PC-07", entry.ActorName);
        Assert.Equal("1234", AuditText.Deserialize(entry.Values)["task"]);
        Assert.Equal("Install", AuditText.Deserialize(entry.Values)["kind"]);
        Assert.DoesNotContain("Aufgabe", entry.Values, StringComparison.Ordinal);

        // Angezeigt: Satz aus der resx-Datei
        Assert.Equal("Aufgabe 1234 (Install Mozilla.Firefox): Succeeded, Rückgabewert 0", AuditText.Describe(entry));
        Assert.Equal("Gerät 12 (PC-07)", AuditText.Actor(entry));
    }

    [Fact]
    public void MissingValuesAndUnknownCodesStayReadable()
    {
        var withoutGroup = Entry(AuditEvents.CodeCreate, AuditText.Serialize([("group", null)]));
        Assert.Equal($"Einmal-Code erstellt (Gruppe: {AuditEvents.None})", AuditText.Describe(withoutGroup));

        // Ein Wert fehlt ganz (z. B. älterer Eintrag): Platzhalter wird zu „?“ statt zu einer Ausnahme
        Assert.Equal("Gruppe „?“ angelegt", AuditText.Describe(Entry(AuditEvents.GroupCreate, "{}")));

        // Unbekannter Code (z. B. aus einer neueren Version) und kaputtes JSON: roh anzeigen, nicht abstürzen
        Assert.Equal("device.wipe, a=1", AuditText.Describe(Entry("device.wipe", """{"a":"1"}""")));
        Assert.Equal("device.wipe", AuditText.Describe(Entry("device.wipe", "kein json")));
    }

    [Fact]
    public void ActorsAreShownByKind()
    {
        Assert.Equal("anna", AuditText.Actor(Entry(AuditEvents.UserLogin, "{}", AuditActor.User("anna"))));
        Assert.Equal(ServerTexts.Audit_System, AuditText.Actor(Entry(AuditEvents.TaskTimeout, "{}", AuditActor.System)));
        Assert.Equal(ServerTexts.Audit_Unknown, AuditText.Actor(Entry(AuditEvents.UserLoginFailed, "{}", AuditActor.Anonymous)));
    }

    [Fact]
    public void TextsOnlyUsePlaceholdersTheCodeFillsIn()
    {
        // Tippfehler wie {pakage} würden sonst still als „?“ erscheinen
        string[] known = ["user", "role", "address", "group", "groupId", "codeBy", "device", "deviceName", "package", "kind", "task", "state", "exitCode"];
        foreach (var action in AuditEvents.All)
        {
            var template = ServerTexts.ResourceManager.GetString(AuditText.TextKey(action), CultureInfo.InvariantCulture)!;
            foreach (Match match in Placeholder().Matches(template))
            {
                Assert.Contains(match.Groups[1].Value, known);
            }
        }
    }

    private static AuditEntry Entry(string action, string values, AuditActor? actor = null)
    {
        var who = actor ?? AuditActor.System;
        return new AuditEntry { ActorKind = who.Kind, ActorName = who.Name, ActorDeviceId = who.DeviceId, Action = action, Values = values };
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
