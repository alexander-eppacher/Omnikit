using Omnikit.Modules.Performance.Data;
using Omnikit.Modules.Performance.Resources;
using static Omnikit.Modules.Performance.Monitoring.Units;

namespace Omnikit.Modules.Performance.Monitoring;

/// <summary>Verständliche Namen für die Karten, z. B. "Laufwerk C: (SSD)" statt "PhysicalDisk 0 C:".</summary>
public static class Naming
{
    /// <summary>"Laufwerk C: (SSD)", "Laufwerk D: E: (Festplatte)" oder ohne Buchstaben "Datenträger 1".</summary>
    public static string Disk(DiskSample disk, DiskKind kind)
    {
        var name = disk.Letters.Count > 0
            ? Format(PerformanceTexts.Disk_Title, string.Join(" ", disk.Letters))
            : Format(PerformanceTexts.Disk_TitleNumber, disk.Number);

        return kind switch
        {
            DiskKind.Ssd => $"{name} ({PerformanceTexts.Disk_Ssd})",
            DiskKind.Hdd => $"{name} ({PerformanceTexts.Disk_Hdd})",
            _ => name,
        };
    }

    /// <summary>Erst ab diesem Unterschied zwischen eingebaut und nutzbar lohnt sich der Hinweis (VMs reservieren oft nur wenige MB).</summary>
    public const long ReservedMemoryNoteFromBytes = 512L * 1024 * 1024;

    /// <summary>
    /// Der Task-Manager zeigt oben den eingebauten Speicher (z. B. 32 GB), Windows kann aber weniger nutzen (z. B. 27,3 GB),
    /// weil die Prozessorgrafik einen Teil reserviert. Damit niemand einen Fehler vermutet, erklären wir den Unterschied.
    /// </summary>
    public static string? ReservedMemoryNote(long? installedBytes, long usableBytes) =>
        installedBytes is long installed && installed - usableBytes >= ReservedMemoryNoteFromBytes
            ? Format(PerformanceTexts.Memory_Reserved, Bytes(installed))
            : null;

    /// <summary>Kurzname für Hinweise, z. B. "C:" oder "Datenträger 1".</summary>
    public static string DiskShort(DiskSample disk) =>
        disk.Letters.Count > 0 ? string.Join(" ", disk.Letters) : Format(PerformanceTexts.Disk_TitleNumber, disk.Number);
}
