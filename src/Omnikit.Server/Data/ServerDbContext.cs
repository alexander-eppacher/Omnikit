using Microsoft.EntityFrameworkCore;

namespace Omnikit.Server.Data;

/// <summary>Zugriff auf die Server-Datenbank (PostgreSQL im Betrieb, SQLite in Tests).</summary>
public sealed class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<DeviceGroup> Groups => Set<DeviceGroup>();

    public DbSet<GroupPackage> GroupPackages => Set<GroupPackage>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<RegistrationCode> RegistrationCodes => Set<RegistrationCode>();

    public DbSet<DeviceTask> Tasks => Set<DeviceTask>();

    public DbSet<AuditEntry> Audit => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(user => user.UserName).IsUnique();
        modelBuilder.Entity<Device>().HasIndex(device => device.TokenHash).IsUnique();
        modelBuilder.Entity<RegistrationCode>().HasIndex(code => code.CodeHash).IsUnique();

        // Ein Code gilt genau einmal: Beim Speichern prüft die Datenbank, ob „benutzt am“ noch leer war.
        // Lösen zwei Anfragen denselben Code gleichzeitig ein, scheitert die zweite (DbUpdateConcurrencyException).
        modelBuilder.Entity<RegistrationCode>().Property(code => code.UsedAt).IsConcurrencyToken();
        modelBuilder.Entity<DeviceGroup>().HasIndex(group => group.Name).IsUnique();
        modelBuilder.Entity<GroupPackage>().HasIndex(package => new { package.GroupId, package.PackageId }).IsUnique();

        // Gruppe gelöscht: Geräte bleiben, nur ohne Gruppe
        modelBuilder.Entity<Device>().HasOne(device => device.Group).WithMany(group => group.Devices).OnDelete(DeleteBehavior.SetNull);

        // Gerät gelöscht: seine Aufgaben auch (das Audit-Log bleibt)
        modelBuilder.Entity<DeviceTask>().HasOne(task => task.Device).WithMany(device => device.Tasks).OnDelete(DeleteBehavior.Cascade);
    }
}
