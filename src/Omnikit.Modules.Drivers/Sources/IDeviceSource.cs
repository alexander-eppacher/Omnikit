using Omnikit.Modules.Drivers.Data;

namespace Omnikit.Modules.Drivers.Sources;

/// <summary>Geräte und Treiber. Hinter einem Interface, damit das Modul in Tests mit festen Werten läuft.</summary>
public interface IDeviceSource
{
    /// <returns><c>null</c>, wenn die Liste nicht gelesen werden konnte.</returns>
    IReadOnlyList<DeviceInfo>? ReadDevices();

    /// <returns>Leer, wenn die Liste nicht gelesen werden konnte.</returns>
    IReadOnlyList<DriverInfo> ReadDrivers();
}
