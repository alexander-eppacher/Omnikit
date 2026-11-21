using Omnikit.Core.Modules;
using Omnikit.Shared;

namespace Omnikit.Server.Data;

// Tabellen der Server-Datenbank. Zeitpunkte immer in UTC.

/// <summary>Rollen der Verwaltung: Admin darf alles, Betrachter nur ansehen.</summary>
public enum UserRole
{
    Viewer,
    Admin,
}

public sealed class User
{
    public int Id { get; set; }

    public required string UserName { get; set; }

    /// <summary>PBKDF2-Hash vom PasswordHasher, nie das Passwort selbst.</summary>
    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Gruppe von Geräten, z. B. „Computerraum 1“, mit Pflichtprogrammen.</summary>
public sealed class DeviceGroup
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public List<GroupPackage> RequiredPackages { get; set; } = [];

    public List<Device> Devices { get; set; } = [];
}

/// <summary>Pflichtprogramm einer Gruppe (winget-Paket-ID).</summary>
public sealed class GroupPackage
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public required string PackageId { get; set; }
}

/// <summary>Ein registrierter PC.</summary>
public sealed class Device
{
    public int Id { get; set; }

    /// <summary>Computername, wie ihn der Agent meldet.</summary>
    public required string Name { get; set; }

    /// <summary>SHA-256 des Geräte-Tokens (nie der Token selbst).</summary>
    public required string TokenHash { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public int? GroupId { get; set; }

    public DeviceGroup? Group { get; set; }

    /// <summary>Gesamtampel des letzten Berichts.</summary>
    public Severity? LastSeverity { get; set; }

    public string? OsName { get; set; }

    public string? Model { get; set; }

    public bool IsVirtualMachine { get; set; }

    /// <summary>Letzter Bericht als JSON (gleiches Format wie der Export der App).</summary>
    public string? LastReportJson { get; set; }

    /// <summary>Installierte Programme laut letztem Bericht, als JSON; null = unbekannt.</summary>
    public string? InstalledJson { get; set; }

    public List<DeviceTask> Tasks { get; set; } = [];
}

/// <summary>Einmal-Code zum Registrieren eines Agents.</summary>
public sealed class RegistrationCode
{
    public int Id { get; set; }

    /// <summary>SHA-256 des Codes.</summary>
    public required string CodeHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    /// <summary>Gruppe, in die das Gerät kommt (optional).</summary>
    public int? GroupId { get; set; }

    public required string CreatedBy { get; set; }
}

public enum TaskState
{
    /// <summary>Angelegt, wartet auf Freigabe durch einen Admin.</summary>
    AwaitingApproval,

    /// <summary>Freigegeben; der Agent holt sie beim nächsten Abfragen ab.</summary>
    Approved,

    /// <summary>Vom Agent abgeholt, läuft.</summary>
    Running,

    Succeeded,
    Failed,

    /// <summary>Von einem Admin abgelehnt; wird nie ausgeführt.</summary>
    Rejected,
}

/// <summary>Aufgabe für ein Gerät (nur Programme installieren/aktualisieren).</summary>
public sealed class DeviceTask
{
    public int Id { get; set; }

    public int DeviceId { get; set; }

    public Device? Device { get; set; }

    public AgentTaskKind Kind { get; set; }

    public required string PackageId { get; set; }

    public TaskState State { get; set; }

    public required string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public int? ExitCode { get; set; }

    public string? ResultMessage { get; set; }
}

/// <summary>Wer hat wann was geändert. Wird nie geändert oder gelöscht.</summary>
public sealed class AuditEntry
{
    public int Id { get; set; }

    public DateTime At { get; set; }

    /// <summary>Benutzername oder „Gerät 12“.</summary>
    public required string Actor { get; set; }

    /// <summary>Kurzname der Aktion, z. B. „task.approve“.</summary>
    public required string Action { get; set; }

    public required string Details { get; set; }
}
