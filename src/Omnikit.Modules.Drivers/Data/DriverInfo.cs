namespace Omnikit.Modules.Drivers.Data;

/// <summary>Installierter Treiber eines Geräts (WMI <c>Win32_PnPSignedDriver</c>).</summary>
/// <param name="DeviceId">Geräte-ID, passt zu <see cref="DeviceInfo.DeviceId"/>.</param>
/// <param name="DeviceClass">Geräteklasse in Großbuchstaben, z. B. „DISPLAY“.</param>
/// <param name="DeviceName">Anzeigename des Geräts.</param>
/// <param name="Provider">Wer den Treiber liefert, z. B. „Intel“ oder „Microsoft“.</param>
/// <param name="Version">Treiberversion.</param>
/// <param name="Date">Datum des Treibers (vom Hersteller angegeben).</param>
/// <param name="InfName">Name der Installationsdatei, z. B. „oem12.inf“ oder „display.inf“.</param>
public sealed record DriverInfo(
    string DeviceId,
    string? DeviceClass,
    string? DeviceName,
    string? Provider,
    string? Version,
    DateOnly? Date,
    string? InfName);
