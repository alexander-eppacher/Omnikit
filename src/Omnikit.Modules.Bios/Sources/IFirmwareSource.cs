using Omnikit.Modules.Bios.Data;

namespace Omnikit.Modules.Bios.Sources;

/// <summary>BIOS/UEFI-Daten. Hinter einem Interface, damit das Modul in Tests mit festen Werten läuft.</summary>
public interface IFirmwareSource
{
    /// <returns><c>null</c>, wenn nichts gelesen werden konnte.</returns>
    FirmwareInfo? Read();
}
