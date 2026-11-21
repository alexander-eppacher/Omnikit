using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Server.Data;

using static Omnikit.Server.Tests.TestCancellation;

namespace Omnikit.Server.Tests;

/// <summary>Datenbank-Migrationen mit echten Dateien, wie im Container.</summary>
public sealed class MigrationTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("omnikit-db-").FullName;
    private readonly FakeTime _time = new();

    private string File => Path.Combine(_folder, "omnikit.db");

    private ServerDbContext Open() => new(new DbContextOptionsBuilder<ServerDbContext>().UseSqlite($"Data Source={File}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void EveryModelChangeHasAMigration()
    {
        // Schlägt fehl, wenn jemand Entities ändert und „dotnet ef migrations add …“ vergisst
        using var db = Open();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task NewDatabaseIsCreatedWithoutBackupAndSecondStartChangesNothing()
    {
        await using (var db = Open())
        {
            await DatabaseSetup.MigrateAsync(db, _time, NullLogger.Instance, Ct);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
            db.Users.Add(new User { UserName = "admin", PasswordHash = "x" });
            await db.SaveChangesAsync(Ct);
        }

        await using (var again = Open())
        {
            await DatabaseSetup.MigrateAsync(again, _time, NullLogger.Instance, Ct);
            Assert.Equal(1, await again.Users.CountAsync(Ct));
        }

        Assert.Equal(["omnikit.db"], Directory.GetFiles(_folder).Select(Path.GetFileName).Where(name => !name!.EndsWith("-wal", StringComparison.Ordinal) && !name.EndsWith("-shm", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task DatabaseFromANewerVersionIsRefused()
    {
        await using var db = Open();
        await DatabaseSetup.MigrateAsync(db, _time, NullLogger.Instance, Ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_Future', '10.0.0')", Ct);

        var error = await Assert.ThrowsAsync<DatabaseVersionException>(() => DatabaseSetup.MigrateAsync(db, _time, NullLogger.Instance, Ct));
        Assert.Contains("29991231000000_Future", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseFromAPrereleaseWithoutMigrationsIsRefused()
    {
        await using var db = Open();
        await db.Database.EnsureCreatedAsync(Ct);   // so legten Vorabversionen die Tabellen an

        await Assert.ThrowsAsync<DatabaseVersionException>(() => DatabaseSetup.MigrateAsync(db, _time, NullLogger.Instance, Ct));
    }

    [Fact]
    public async Task BackupContainsTheDataEvenInWalMode()
    {
        await using var db = Open();
        await DatabaseSetup.MigrateAsync(db, _time, NullLogger.Instance, Ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", Ct);
        db.Users.Add(new User { UserName = "anna", PasswordHash = "x" });
        await db.SaveChangesAsync(Ct);

        var backup = DatabaseSetup.Backup(db, _time);

        Assert.Equal(File + ".vor-20261102-120000", backup);
        await using var copy = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>().UseSqlite($"Data Source={backup}").Options);
        Assert.Equal("anna", (await copy.Users.SingleAsync(Ct)).UserName);
    }
}
