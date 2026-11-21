namespace Omnikit.Modules.Drivers.Data;

/// <summary>Ein Gerät, wie es der Geräte-Manager zeigt (WMI <c>Win32_PnPEntity</c>).</summary>
/// <param name="Name">Anzeigename, z. B. „Intel(R) Wi-Fi 6 AX201“.</param>
/// <param name="DeviceClass">Geräteklasse, z. B. „Net“ oder „Display“; bei Geräten ohne Treiber oft leer.</param>
/// <param name="DeviceId">Geräte-ID, z. B. „PCI\VEN_8086&amp;DEV_A0F0…“.</param>
/// <param name="ErrorCode">Fehlercode des Geräte-Managers; 0 = funktioniert.</param>
public sealed record DeviceInfo(string Name, string? DeviceClass, string DeviceId, int ErrorCode);
