using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Omnikit.Server.Data;

/// <summary>Die Datenbank passt nicht zu diesem Programm; der Server startet dann nicht (lieber gar nicht als mit kaputten Daten).</summary>
public sealed class DatabaseVersionException(string message) : Exception(message);

/// <summary>
/// Bringt die Datenbank beim Start auf den Stand dieses Programms (EF-Core-Migrationen, 0017).
/// Vor jeder Änderung wird die Datei gesichert. Ist die Datenbank neuer als das Programm (Rückkehr zu einer älteren Version)
/// oder stammt sie aus einer Vorabversion ohne Migrationen, startet der Server nicht.
/// </summary>
public static class DatabaseSetup
{
    public static async Task MigrateAsync(ServerDbContext db, TimeProvider time, ILogger logger, CancellationToken cancellationToken)
    {
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        var unknown = applied.Where(id => !known.Contains(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new DatabaseVersionException(
                $"Die Datenbank wurde von einer neueren Omnikit-Version geändert ({string.Join(", ", unknown)}). " +
                "Bitte diese neuere Version verwenden oder die Sicherung von vorher zurückspielen.");
        }

        if (applied.Count == 0 && await HasTablesAsync(db, cancellationToken))
        {
            throw new DatabaseVersionException(
                "Die Datenbank stammt aus einer Vorabversion ohne Migrationen und kann nicht übernommen werden. " +
                "Bitte die Datei löschen; beim nächsten Start entsteht eine neue.");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        if (applied.Count > 0 && Backup(db, time) is { } backup)
        {
            logger.LogInformation("Datenbank gesichert nach {Backup}", backup);
        }

        logger.LogInformation("Datenbank wird aktualisiert: {Migrations}", string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>Kopiert die Datenbankdatei neben sich („omnikit.db.vor-20261111-120000“); null bei einer Datenbank im Arbeitsspeicher.</summary>
    internal static string? Backup(ServerDbContext db, TimeProvider time)
    {
        var path = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (string.IsNullOrEmpty(path) || path == ":memory:" || !File.Exists(path))
        {
            return null;
        }

        var target = $"{path}.vor-{time.GetUtcNow():yyyyMMdd-HHmmss}";

        // Über SQLite selbst sichern statt die Datei zu kopieren: im WAL-Modus stünde sonst ein Teil der Daten nur in der -wal-Datei
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            connection.Open();
        }

        try
        {
            using var destination = new SqliteConnection($"Data Source={target};Pooling=False");
            destination.Open();
            connection.BackupDatabase(destination);
        }
        finally
        {
            if (!wasOpen)
            {
                connection.Close();
            }
        }

        return target;
    }

    private static async Task<bool> HasTablesAsync(ServerDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'Users'";
            return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
