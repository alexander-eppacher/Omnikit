using System.Globalization;
using System.Runtime.Versioning;
using Omnikit.Core.Elevation;
using Omnikit.Core.Windows;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>Ein USB-Laufwerk, auf das ein Stick geschrieben werden kann.</summary>
/// <param name="Number">Datenträgernummer in Windows (wie in der Datenträgerverwaltung). Kann sich beim Umstecken ändern.</param>
/// <param name="Name">Gerätename, z. B. "SanDisk Ultra".</param>
/// <param name="DevicePath">
/// Gerätepfad von Windows (<c>MSFT_Disk.Path</c>, beginnt mit <c>\\?\</c>). Er gehört fest zu genau diesem angeschlossenen
/// Gerät – auch bei Sticks ohne Seriennummer – und ist deshalb das, woran der Hilfsprozess den Stick wiedererkennt.
/// </param>
/// <param name="IsRemovable">Windows meldet das Laufwerk als Wechselmedium (normale USB-Sticks). USB-Festplatten und -SSDs nicht.</param>
public sealed record UsbDisk(int Number, string Name, long SizeBytes, string DevicePath, bool IsRemovable)
{
    public string DisplayName => string.Format(
        CultureInfo.CurrentCulture, IsRemovable ? Texts.Usb_Display : Texts.Usb_DisplayFixed, Name, SizeBytes / 1e9);

    public override string ToString() => DisplayName;
}

/// <summary>
/// Findet USB-Laufwerke und prüft, ob eines gelöscht werden darf. Die Prüfung läuft im Hilfsprozess noch einmal,
/// unabhängig von der App: nur USB, nie das System- oder Startlaufwerk, höchstens 256 GB und – außer der Nutzer
/// hat USB-Festplatten ausdrücklich zugelassen – nur Wechselmedien.
/// </summary>
public static class UsbDiskFinder
{
    /// <summary>Größere Laufwerke sind meist externe Festplatten mit Daten – die löscht Omnikit nicht.</summary>
    public const long MaxSizeBytes = 256L * 1000 * 1000 * 1000;

    /// <summary>BusType in MSFT_Disk: 7 = USB.</summary>
    internal const int BusTypeUsb = 7;

    /// <summary>So nennt Win32_DiskDrive ein Wechselmedium (aus dem „RemovableMedia“-Merkmal des Geräts).</summary>
    internal const string RemovableMediaType = "Removable Media";

    /// <summary>Gerätepfade sind lang, aber nicht beliebig lang; alles darüber ist sicher kein echter Pfad.</summary>
    internal const int MaxDevicePathLength = 512;

    /// <param name="includeFixed">Auch USB-Laufwerke zeigen, die sich als Festplatte melden (USB-SSDs, manche schnelle Sticks).</param>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<UsbDisk> List(bool includeFixed) =>
        Query().Where(row => Allowed(row, includeFixed) is null).Select(ToDisk).ToList();

    /// <summary>
    /// Prüft im Hilfsprozess, ob genau dieser Datenträger (Gerätepfad, Nummer und Größe wie vom Nutzer bestätigt) gelöscht werden darf.
    /// </summary>
    /// <exception cref="ElevatedOperationException">Mit verständlichem Grund, wenn nicht.</exception>
    [SupportedOSPlatform("windows")]
    public static UsbDisk Check(string devicePath, int number, long confirmedSizeBytes, bool allowFixed)
    {
        var row = Query().FirstOrDefault(row => string.Equals(row.GetValueOrDefault("Path") as string, devicePath, StringComparison.OrdinalIgnoreCase))
            ?? throw new ElevatedOperationException(Texts.Create_DiskGone);
        if (Allowed(row, allowFixed) is { } reason)
        {
            throw new ElevatedOperationException(reason);
        }

        var disk = ToDisk(row);
        if (disk.Number != number || disk.SizeBytes != confirmedSizeBytes)
        {
            // Nummern ändern sich, wenn Sticks umgesteckt werden: dann lieber abbrechen als den falschen löschen.
            throw new ElevatedOperationException(Texts.Create_DiskChanged);
        }

        return disk;
    }

    /// <summary>
    /// Grund, warum der Datenträger nicht in Frage kommt; <c>null</c> = erlaubt.
    /// Fehlt ein Wert (Windows liefert ihn nicht), gilt das immer als „nicht erlaubt“ – nie raten.
    /// </summary>
    internal static string? Allowed(IReadOnlyDictionary<string, object?> row, bool allowFixed) =>
        Wmi.ToLong(row.GetValueOrDefault("BusType")) != BusTypeUsb ? Texts.Create_NotUsb
        : row.GetValueOrDefault("IsBoot") is not false || row.GetValueOrDefault("IsSystem") is not false ? Texts.Create_SystemDisk
        : Wmi.ToLong(row.GetValueOrDefault("Size")) is not long size || size <= 0 || size > MaxSizeBytes ? Texts.Create_TooLarge
        : row.GetValueOrDefault("IsReadOnly") is true ? Texts.Create_ReadOnly
        : !IsDevicePath(row.GetValueOrDefault("Path") as string) ? Texts.Create_DiskGone
        : !allowFixed && row.GetValueOrDefault("MediaType") as string != RemovableMediaType ? Texts.Create_NotRemovable
        : null;

    /// <summary>
    /// Liegt die Datei auf einer Partition dieses Datenträgers? Dann würde sie beim Löschen zerstört, bevor sie gelesen ist
    /// (ISO auf dem Ziel-Stick) – oder Omnikit selbst wurde von diesem Stick gestartet.
    /// Verglichen wird die Volume-Kennung („\\?\Volume{…}\“), nicht der Laufwerksbuchstabe: So zählen auch Bereitstellungspunkte,
    /// <c>subst</c>-Laufwerke und Junctions, die auf den Stick zeigen.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool HoldsFile(UsbDisk disk, string file)
    {
        var accessPaths = Wmi.Query(Wmi.StorageNamespace, $"SELECT AccessPaths FROM MSFT_Partition WHERE DiskNumber = {disk.Number.ToString(CultureInfo.InvariantCulture)}")
            .SelectMany(partition => partition.GetValueOrDefault("AccessPaths") as string[] ?? []);
        return IsOnVolume(VolumePaths.FinalPath(file), accessPaths);
    }

    /// <summary>Beginnt der aufgelöste Pfad mit einer der Volume-Kennungen? (reine Funktion, für Tests)</summary>
    internal static bool IsOnVolume(string finalPath, IEnumerable<string> accessPaths) =>
        accessPaths
            .Where(path => path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
            .Any(volume => finalPath.StartsWith(volume.EndsWith('\\') ? volume : volume + "\\", StringComparison.OrdinalIgnoreCase));

    /// <summary>Sieht wie ein Gerätepfad von Windows aus (<c>\\?\…</c>, ohne Steuerzeichen, begrenzte Länge).</summary>
    internal static bool IsDevicePath(string? path) =>
        path is { Length: > 4 and <= MaxDevicePathLength }
        && path.StartsWith(@"\\?\", StringComparison.Ordinal)
        && !path.Any(char.IsControl);

    private static UsbDisk ToDisk(IReadOnlyDictionary<string, object?> row) => new(
        Convert.ToInt32(row["Number"], CultureInfo.InvariantCulture),
        (row.GetValueOrDefault("FriendlyName") as string)?.Trim() ?? "USB",
        Wmi.ToLong(row.GetValueOrDefault("Size")) ?? 0,
        (string)row["Path"]!,
        row.GetValueOrDefault("MediaType") as string == RemovableMediaType);

    /// <summary>MSFT_Disk (Storage-API) und dazu das Wechselmedium-Merkmal aus Win32_DiskDrive (gleiche Nummer = Index).</summary>
    [SupportedOSPlatform("windows")]
    private static List<Dictionary<string, object?>> Query()
    {
        var mediaTypes = Wmi.Query("SELECT Index, MediaType FROM Win32_DiskDrive")
            .Where(drive => Wmi.ToLong(drive.GetValueOrDefault("Index")) is not null)
            .GroupBy(drive => Wmi.ToLong(drive["Index"])!.Value)
            .ToDictionary(group => group.Key, group => group.First().GetValueOrDefault("MediaType") as string);
        var disks = Wmi.Query(Wmi.StorageNamespace, "SELECT Number, FriendlyName, Size, BusType, IsBoot, IsSystem, IsReadOnly, Path FROM MSFT_Disk");
        foreach (var disk in disks)
        {
            disk["MediaType"] = Wmi.ToLong(disk.GetValueOrDefault("Number")) is long number ? mediaTypes.GetValueOrDefault(number) : null;
        }

        return disks;
    }
}
