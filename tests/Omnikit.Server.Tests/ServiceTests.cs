using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Omnikit.Core.Modules;
using Omnikit.Core.Reports;
using Omnikit.Server.Data;
using Omnikit.Server.Security;
using Omnikit.Server.Services;
using Omnikit.Shared;

using static Omnikit.Server.Tests.TestCancellation;

namespace Omnikit.Server.Tests;

public sealed class RegistrationTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private RegistrationService Service()
    {
        var (db, audit) = _database.Open();
        return new RegistrationService(db, audit, _database.Time);
    }

    [Fact]
    public async Task CodeWorksExactlyOnce()
    {
        var code = await Service().CreateCodeAsync("admin", null, Ct);

        var first = await Service().RegisterAsync(new RegisterRequest(code, "PC-01"), Ct);
        var second = await Service().RegisterAsync(new RegisterRequest(code, "PC-02"), Ct);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task ExpiredCodeIsRejected()
    {
        var code = await Service().CreateCodeAsync("admin", null, Ct);
        _database.Time.Now += RegistrationService.CodeLifetime + TimeSpan.FromMinutes(1);

        Assert.Null(await Service().RegisterAsync(new RegisterRequest(code, "PC-01"), Ct));
    }

    [Fact]
    public async Task CodeCanBeTypedInLowercaseWithoutDashes()
    {
        var code = await Service().CreateCodeAsync("admin", null, Ct);

        var typed = code.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant();
        Assert.NotNull(await Service().RegisterAsync(new RegisterRequest(typed, "PC-01"), Ct));
    }

    [Fact]
    public async Task OnlyHashesAreStored()
    {
        var code = await Service().CreateCodeAsync("admin", null, Ct);
        var response = await Service().RegisterAsync(new RegisterRequest(code, "PC-01"), Ct);

        using var db = _database.CreateContext();
        var device = await db.Devices.SingleAsync(Ct);
        Assert.NotEqual(response!.Token, device.TokenHash);
        Assert.Equal(SecretTokens.Hash(response.Token), device.TokenHash);
        Assert.DoesNotContain(db.RegistrationCodes, stored => stored.CodeHash.Contains(code, StringComparison.Ordinal));
        Assert.Contains(db.Audit, entry => entry.Action == "device.register");
    }

    [Fact]
    public async Task CodeWithGroupPutsDeviceIntoTheGroup()
    {
        using (var db = _database.CreateContext())
        {
            db.Groups.Add(new DeviceGroup { Name = "Raum 1" });
            await db.SaveChangesAsync(Ct);
        }

        var code = await Service().CreateCodeAsync("admin", 1, Ct);
        await Service().RegisterAsync(new RegisterRequest(code, "PC-01"), Ct);

        using var check = _database.CreateContext();
        Assert.Equal(1, (await check.Devices.SingleAsync(Ct)).GroupId);
    }

    [Fact]
    public async Task CodeOfADeletedGroupIsNoLongerValid()
    {
        using (var db = _database.CreateContext())
        {
            db.Groups.Add(new DeviceGroup { Name = "Raum 1" });
            await db.SaveChangesAsync(Ct);
        }

        var code = await Service().CreateCodeAsync("admin", 1, Ct);
        using (var db = _database.CreateContext())
        {
            db.Groups.Remove(await db.Groups.SingleAsync(Ct));
            await db.SaveChangesAsync(Ct);
        }

        Assert.Null(await Service().RegisterAsync(new RegisterRequest(code, "PC-01"), Ct));

        using var check = _database.CreateContext();
        Assert.Empty(await check.Devices.ToListAsync(Ct));
    }

    [Fact]
    public async Task CodeUsedInBetweenCannotBeUsedAgain()
    {
        var code = await Service().CreateCodeAsync("admin", null, Ct);

        // Zwei Anfragen lesen den Code gleichzeitig als „noch frei“ …
        using var slow = _database.CreateContext();
        var seenBySlow = await slow.RegistrationCodes.SingleAsync(Ct);
        Assert.NotNull(await Service().RegisterAsync(new RegisterRequest(code, "PC-01"), Ct));

        // … die langsamere darf ihn danach nicht mehr als benutzt speichern
        seenBySlow.UsedAt = _database.Time.Now.UtcDateTime;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => slow.SaveChangesAsync(Ct));
    }

    [Fact]
    public void CodesAreReadableAndRandom()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => SecretTokens.NewCode()).ToList();

        Assert.Equal(50, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[A-Z2-9]{5}(-[A-Z2-9]{5}){3}$", code));
        Assert.All(codes, code => Assert.DoesNotContain('O', code));
    }
}

public sealed class TaskFlowTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<Device> AddDeviceAsync(string name = "PC-01", int? groupId = null, IReadOnlyList<InstalledProgram>? installed = null)
    {
        using var db = _database.CreateContext();
        var device = new Device
        {
            Name = name,
            TokenHash = SecretTokens.Hash(name),
            GroupId = groupId,
            InstalledJson = installed is null ? null : JsonSerializer.Serialize(installed, AgentApi.JsonOptions),
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync(Ct);
        return device;
    }

    private TaskService Tasks()
    {
        var (db, audit) = _database.Open();
        return new TaskService(db, audit, _database.Time);
    }

    private AgentService Agents()
    {
        var (db, audit) = _database.Open();
        return new AgentService(db, audit, _database.Time);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--source evil")]
    [InlineData("7zip.7zip; rm -rf /")]
    public async Task InvalidPackageIdsAreRejected(string packageId)
    {
        var device = await AddDeviceAsync();

        var result = await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, packageId, Ct);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task TaskRunsOnlyAfterApprovalAndOnlyOnce()
    {
        var device = await AddDeviceAsync();
        await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, "7zip.7zip", Ct);

        Assert.Empty(await Agents().TakeTasksAsync(device, Ct));

        Assert.True((await Tasks().ApproveAsync("admin", 1, Ct)).Success);
        var taken = Assert.Single(await Agents().TakeTasksAsync(device, Ct));
        Assert.Equal("7zip.7zip", taken.PackageId);
        Assert.Empty(await Agents().TakeTasksAsync(device, Ct));

        Assert.True(await Agents().SaveResultAsync(device, taken.Id, new TaskResultRequest(true, 0, "fertig"), Ct));
        using var db = _database.CreateContext();
        Assert.Equal(TaskState.Succeeded, (await db.Tasks.SingleAsync(Ct)).State);
    }

    [Fact]
    public async Task TaskWithoutAnswerFailsWhenTheAgentAsksAgain()
    {
        // PC wurde mitten in der Installation ausgeschaltet: Das Ergebnis kommt nie, der Agent fragt später neu
        var device = await AddDeviceAsync();
        await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, "7zip.7zip", Ct);
        await Tasks().ApproveAsync("admin", 1, Ct);
        await Agents().TakeTasksAsync(device, Ct);

        _database.Time.Now += AgentService.AnswerTimeout - TimeSpan.FromMinutes(1);
        Assert.Empty(await Agents().TakeTasksAsync(device, Ct));
        using (var early = _database.CreateContext())
        {
            Assert.Equal(TaskState.Running, (await early.Tasks.SingleAsync(Ct)).State);
        }

        _database.Time.Now += TimeSpan.FromMinutes(2);
        Assert.Empty(await Agents().TakeTasksAsync(device, Ct));

        using var db = _database.CreateContext();
        var task = await db.Tasks.SingleAsync(Ct);
        Assert.Equal(TaskState.Failed, task.State);
        Assert.NotNull(task.FinishedAt);
        Assert.False(string.IsNullOrEmpty(task.ResultMessage));
        Assert.Contains(await db.Audit.ToListAsync(Ct), entry => entry.Action == "task.result");
        // Ein verspätetes Ergebnis ändert daran nichts mehr
        Assert.False(await Agents().SaveResultAsync(device, task.Id, new TaskResultRequest(true, 0, "fertig"), Ct));
    }

    [Fact]
    public async Task OnlyOneTaskIsHandedOutAtATime()
    {
        var device = await AddDeviceAsync();
        await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, "7zip.7zip", Ct);
        await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, "VideoLAN.VLC", Ct);
        await Tasks().ApproveAsync("admin", 1, Ct);
        await Tasks().ApproveAsync("admin", 2, Ct);

        var first = Assert.Single(await Agents().TakeTasksAsync(device, Ct));
        _database.Time.Now += TimeSpan.FromMinutes(25);
        await Agents().SaveResultAsync(device, first.Id, new TaskResultRequest(true, 0, "fertig"), Ct);
        var second = Assert.Single(await Agents().TakeTasksAsync(device, Ct));

        Assert.Equal("7zip.7zip", first.PackageId);
        Assert.Equal("VideoLAN.VLC", second.PackageId);
        // Die Zeitgrenze der zweiten Aufgabe beginnt erst jetzt: nach weiteren 25 Minuten läuft sie noch
        _database.Time.Now += TimeSpan.FromMinutes(25);
        Assert.Equal(0, await Agents().ExpireLostTasksAsync(null, Ct));
    }

    [Fact]
    public async Task TasksOfDevicesThatNeverComeBackExpireToo()
    {
        var device = await AddDeviceAsync();
        await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, "7zip.7zip", Ct);
        await Tasks().ApproveAsync("admin", 1, Ct);
        await Agents().TakeTasksAsync(device, Ct);

        _database.Time.Now += AgentService.AnswerTimeout + TimeSpan.FromMinutes(1);

        Assert.Equal(1, await Agents().ExpireLostTasksAsync(null, Ct));
        using var db = _database.CreateContext();
        Assert.Equal(TaskState.Failed, (await db.Tasks.SingleAsync(Ct)).State);
    }

    [Fact]
    public async Task RunningTasksOfOtherDevicesStayUntouched()
    {
        var mine = await AddDeviceAsync("PC-01");
        var other = await AddDeviceAsync("PC-02");
        await Tasks().CreateAsync("admin", other.Id, AgentTaskKind.Install, "7zip.7zip", Ct);
        await Tasks().ApproveAsync("admin", 1, Ct);
        await Agents().TakeTasksAsync(other, Ct);

        _database.Time.Now += AgentService.AnswerTimeout + TimeSpan.FromMinutes(1);
        await Agents().TakeTasksAsync(mine, Ct);

        using var db = _database.CreateContext();
        Assert.Equal(TaskState.Running, (await db.Tasks.SingleAsync(Ct)).State);
    }

    [Fact]
    public async Task RejectedTasksNeverRunAndCannotBeApprovedLater()
    {
        var device = await AddDeviceAsync();
        await Tasks().CreateAsync("admin", device.Id, AgentTaskKind.Install, "7zip.7zip", Ct);

        await Tasks().RejectAsync("admin", 1, Ct);

        Assert.False((await Tasks().ApproveAsync("admin", 1, Ct)).Success);
        Assert.Empty(await Agents().TakeTasksAsync(device, Ct));
    }

    [Fact]
    public async Task DeviceCannotReportResultsForOtherDevices()
    {
        var mine = await AddDeviceAsync("PC-01");
        var other = await AddDeviceAsync("PC-02");
        await Tasks().CreateAsync("admin", other.Id, AgentTaskKind.Install, "7zip.7zip", Ct);
        await Tasks().ApproveAsync("admin", 1, Ct);
        await Agents().TakeTasksAsync(other, Ct);

        Assert.False(await Agents().SaveResultAsync(mine, 1, new TaskResultRequest(true, 0, "fertig"), Ct));
    }

    [Fact]
    public async Task GroupTasksOnlyForMissingProgramsWithoutDuplicates()
    {
        using (var db = _database.CreateContext())
        {
            db.Groups.Add(new DeviceGroup
            {
                Name = "Raum 1",
                RequiredPackages = [new GroupPackage { PackageId = "7zip.7zip" }, new GroupPackage { PackageId = "VideoLAN.VLC" }],
            });
            await db.SaveChangesAsync(Ct);
        }

        await AddDeviceAsync("PC-01", 1, [new InstalledProgram("7zip.7zip", "24.09")]);
        await AddDeviceAsync("PC-02", 1, []);
        await AddDeviceAsync("PC-03", 1, installed: null); // noch keine Programmliste

        Assert.Equal(3, await Tasks().CreateMissingForGroupAsync("admin", 1, Ct));
        Assert.Equal(0, await Tasks().CreateMissingForGroupAsync("admin", 1, Ct));

        using var check = _database.CreateContext();
        Assert.All(check.Tasks, task => Assert.Equal(TaskState.AwaitingApproval, task.State));
    }

    [Fact]
    public async Task ReportUpdatesTheDevice()
    {
        var device = await AddDeviceAsync();
        var report = new SystemReport(
            DateTimeOffset.UtcNow, "1.0",
            new ReportSystemInfo("Windows 11 Pro", "24H2", "26100", "LENOVO", "ThinkBook", Core.SystemInfo.VmType.None, true, true),
            [new ReportModule("health", Severity.Warning, [new Finding("SSD", "fast voll", Severity.Warning)])]);

        using (var db = _database.CreateContext())
        {
            var tracked = await db.Devices.SingleAsync(Ct);
            await new AgentService(db, new AuditLog(db, _database.Time), _database.Time)
                .SaveReportAsync(tracked, new ReportRequest("PC-NEU", report, [new InstalledProgram("7zip.7zip", "24.09")]), Ct);
        }

        using var check = _database.CreateContext();
        var saved = await check.Devices.SingleAsync(Ct);
        Assert.Equal("PC-NEU", saved.Name);
        Assert.Equal(Severity.Warning, saved.LastSeverity);
        Assert.Equal("ThinkBook", saved.Model);
        Assert.Equal("fast voll", Display.Report(saved)!.Modules[0].Findings[0].Explanation);
        Assert.Equal("7zip.7zip", Assert.Single(Display.Installed(saved)!).Id);
    }
}

public sealed class UserTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private UserService Users()
    {
        var (db, audit) = _database.Open();
        return new UserService(db, audit, _database.Time);
    }

    [Fact]
    public async Task InitialAdminNeedsALongPassword()
    {
        Assert.False(await Users().EnsureInitialAdminAsync("kurz", Ct));
        Assert.True(await Users().EnsureInitialAdminAsync("ein langes Passwort", Ct));

        Assert.NotNull(await Users().VerifyAsync("admin", "ein langes Passwort", Ct));
        Assert.Null(await Users().VerifyAsync("admin", "falsch falsch falsch", Ct));
        Assert.Null(await Users().VerifyAsync("niemand", "ein langes Passwort", Ct));
    }

    [Fact]
    public async Task PasswordIsNotStoredInPlainText()
    {
        await Users().EnsureInitialAdminAsync("ein langes Passwort", Ct);

        using var db = _database.CreateContext();
        Assert.DoesNotContain("ein langes Passwort", (await db.Users.SingleAsync(Ct)).PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelfAndLastAdminCannotBeDeleted()
    {
        await Users().EnsureInitialAdminAsync("ein langes Passwort", Ct);
        await Users().CreateAsync("admin", "lehrer", "noch ein langes Passwort", UserRole.Viewer, Ct);

        Assert.False((await Users().DeleteAsync("admin", 1, Ct)).Success);
        Assert.False((await Users().DeleteAsync("lehrer", 1, Ct)).Success);
        Assert.True((await Users().DeleteAsync("admin", 2, Ct)).Success);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("")]
    [InlineData("<script>")]
    public async Task BadUserNamesAreRejected(string name)
    {
        Assert.False((await Users().CreateAsync("admin", name, "ein langes Passwort", UserRole.Viewer, Ct)).Success);
    }

    [Fact]
    public async Task UnknownRoleIsRejected()
    {
        Assert.False((await Users().CreateAsync("admin", "lehrer", "ein langes Passwort", (UserRole)7, Ct)).Success);
    }

    [Fact]
    public async Task UserNamesIgnoreUpperAndLowerCase()
    {
        await Users().EnsureInitialAdminAsync("ein langes Passwort", Ct);

        Assert.False((await Users().CreateAsync("admin", "ADMIN", "noch ein langes Passwort", UserRole.Viewer, Ct)).Success);
        Assert.NotNull(await Users().VerifyAsync("Admin", "ein langes Passwort", Ct));
    }

    [Fact]
    public async Task UserNamesAreAsciiOnly()
    {
        Assert.False((await Users().CreateAsync("admin", "jürgen", "ein langes Passwort", UserRole.Viewer, Ct)).Success);
    }
}
