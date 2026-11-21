using System.Globalization;
using Omnikit.Core.Elevation;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>
/// PowerShell-Baustein, der den Stick löscht – und unmittelbar davor, im selben Aufruf, noch einmal prüft, dass es genau
/// der bestätigte Stick ist. Zwischen der Prüfung im Hilfsprozess und dem Löschen können Minuten liegen (Prüfsumme, DISM);
/// in dieser Zeit kann ein Stick abgezogen und unter derselben Nummer ein anderes Laufwerk angesteckt werden.
/// Das Laufwerk wird nur über seinen Gerätepfad angesprochen, nie über die Nummer. Der Pfad kommt als Umgebungsvariable
/// ins Skript, nicht in den Skripttext.
/// </summary>
internal static class EraseScript
{
    public const string DevicePathVariable = "OMNIKIT_DISK_PATH";

    /// <summary>Der Datenträger ist nicht mehr der bestätigte (oder nicht mehr erlaubt): nichts gelöscht.</summary>
    public const int ExitDiskChanged = 21;

    /// <summary>Nach dem Löschen sind noch Partitionen da: abgebrochen, bevor etwas geschrieben wurde.</summary>
    public const int ExitNotErased = 22;

    /// <summary>Prüfen und löschen. Danach steht der geprüfte Datenträger in <c>$d</c>.</summary>
    public static string Build(UsbDisk disk, bool allowFixed)
    {
        var number = disk.Number.ToString(CultureInfo.InvariantCulture);
        var size = disk.SizeBytes.ToString(CultureInfo.InvariantCulture);
        var removable = allowFixed
            ? string.Empty
            : $"if ((Get-CimInstance -ClassName Win32_DiskDrive -Filter 'Index={number}').MediaType -ne '{UsbDiskFinder.RemovableMediaType}') {{ exit {ExitDiskChanged} }}\n";
        return $$"""
            $d = @(Get-Disk -Path $env:{{DevicePathVariable}} -ErrorAction SilentlyContinue)
            if ($d.Count -ne 1) { exit {{ExitDiskChanged}} }
            $d = $d[0]
            if ($d.Number -ne {{number}} -or $d.Size -ne {{size}} -or $d.CimInstanceProperties['BusType'].Value -ne {{UsbDiskFinder.BusTypeUsb}} -or
                $d.IsBoot -ne $false -or $d.IsSystem -ne $false -or $d.IsReadOnly -ne $false) { exit {{ExitDiskChanged}} }
            {{removable}}if ($d.PartitionStyle -ne 'RAW') { $d | Clear-Disk -RemoveData -RemoveOEM -Confirm:$false }
            $d = Get-Disk -Path $env:{{DevicePathVariable}}
            if (@($d | Get-Partition -ErrorAction SilentlyContinue).Count -ne 0) { exit {{ExitNotErased}} }

            """;
    }

    public static IReadOnlyDictionary<string, string> Environment(UsbDisk disk) =>
        new Dictionary<string, string> { [DevicePathVariable] = disk.DevicePath };

    /// <summary>Führt Prüfen + Löschen + <paramref name="then"/> aus; die eigenen Exit-Codes werden zu verständlichen Meldungen.</summary>
    public static async Task<string> RunAsync(UsbDisk disk, bool allowFixed, string then, CancellationToken cancellationToken)
    {
        try
        {
            return await PowerShellCommand.RunAsync(Build(disk, allowFixed) + then, cancellationToken, Environment(disk));
        }
        catch (PowerShellException ex) when (ex.ExitCode is ExitDiskChanged)
        {
            throw new ElevatedOperationException(Texts.Create_DiskChanged, ex);
        }
        catch (PowerShellException ex) when (ex.ExitCode is ExitNotErased)
        {
            throw new ElevatedOperationException(Texts.Create_NotErased, ex);
        }
    }
}
