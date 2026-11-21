using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Omnikit.Server.Data;
using Omnikit.Server.Services;

namespace Omnikit.Server.Tests;

/// <summary>SQLite-Datenbank im Arbeitsspeicher; lebt, solange die Verbindung offen ist.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public FakeTime Time { get; } = new();

    public ServerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ServerDbContext>().UseSqlite(_connection).Options);

    public (ServerDbContext Db, AuditLog Audit) Open()
    {
        var db = CreateContext();
        return (db, new AuditLog(db, Time));
    }

    public void Dispose() => _connection.Dispose();
}

/// <summary>Uhr, die man im Test vorstellen kann.</summary>
internal sealed class FakeTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Abbruchsignal des laufenden Tests (xUnit bricht damit hängende Tests ab).</summary>
internal static class TestCancellation
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}
