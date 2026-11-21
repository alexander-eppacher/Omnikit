using System.Globalization;
using System.Runtime.Versioning;
using Omnikit.Core.Elevation;
using Omnikit.Core.Windows;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>Ein USB-Laufwerk, auf das ein Stick geschrieben werden kann.</summary>
/// <param name="Number">Datenträgernummer in Windows (wie in der Datenträgerverwaltung).</param>
/// <param name="Name">Gerätename, z. B. "SanDisk Ultra".</param>
public sealed record UsbDisk(int Number, string Name, long SizeBytes)
{
    public string DisplayName => string.Format(CultureInfo.CurrentCulture, "{0} ({1:0.0} GB)", Name, SizeBytes / 1e9);

    public override string ToString() => DisplayName;
}

/// <summary>
/// Findet USB-Laufwerke und prüft, ob eines gelöscht werden darf. Die Prüfung läuft im Hilfsprozess noch einmal,
/// unabhängig von der App: nur USB, nie das System- oder Startlaufwerk, höchstens 256 GB.
/// </summary>
public static class UsbDiskFinder
{
    /// <summary>Größere Laufwerke sind meist externe Festplatten mit Daten – die löscht Omnikit nicht.</summary>
    public const long MaxSizeBytes = 256L * 1000 * 1000 * 1000;

    // BusType in MSFT_Disk: 7 = USB
    private const int BusTypeUsb = 7;

    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<UsbDisk> List() =>
        Query().Where(row => Allowed(row) is null).Select(ToDisk).ToList();

    /// <summary>
    /// Prüft im Hilfsprozess, ob genau dieser Datenträger (Nummer und Größe wie vom Nutzer bestätigt) gelöscht werden darf.
    /// </summary>
    /// <exception cref="ElevatedOperationException">Mit verständlichem Grund, wenn nicht.</exception>
    [SupportedOSPlatform("windows")]
    public static UsbDisk Check(int number, long confirmedSizeBytes)
    {
        var row = Query().FirstOrDefault(row => Convert.ToInt32(row["Number"], CultureInfo.InvariantCulture) == number)
            ?? throw new ElevatedOperationException(Texts.Create_DiskGone);

        if (Allowed(row) is { } reason)
        {
            throw new ElevatedOperationException(reason);
        }

        var disk = ToDisk(row);
        if (disk.SizeBytes != confirmedSizeBytes)
        {
            // Nummern ändern sich, wenn Sticks umgesteckt werden: dann lieber abbrechen als den falschen löschen.
            throw new ElevatedOperationException(Texts.Create_DiskChanged);
        }

        return disk;
    }

    /// <returns>Grund, warum der Datenträger nicht in Frage kommt; null = erlaubt.</returns>
    internal static string? Allowed(IReadOnlyDictionary<string, object?> row) =>
        Wmi.ToLong(row.GetValueOrDefault("BusType")) != BusTypeUsb ? Texts.Create_NotUsb
        : row.GetValueOrDefault("IsBoot") is true || row.GetValueOrDefault("IsSystem") is true ? Texts.Create_SystemDisk
        : Wmi.ToLong(row.GetValueOrDefault("Size")) is not long size || size > MaxSizeBytes ? Texts.Create_TooLarge
        : null;

    private static UsbDisk ToDisk(IReadOnlyDictionary<string, object?> row) => new(
        Convert.ToInt32(row["Number"], CultureInfo.InvariantCulture),
        (row.GetValueOrDefault("FriendlyName") as string)?.Trim() ?? "USB",
        Wmi.ToLong(row.GetValueOrDefault("Size")) ?? 0);

    [SupportedOSPlatform("windows")]
    private static List<Dictionary<string, object?>> Query() =>
        Wmi.Query(Wmi.StorageNamespace, "SELECT Number, FriendlyName, Size, BusType, IsBoot, IsSystem FROM MSFT_Disk");
}
