using System.Globalization;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Windows;
using Omnikit.Modules.Drivers.Data;

namespace Omnikit.Modules.Drivers.Sources;

/// <summary>
/// Liest Geräte und Treiber über WMI – dieselben Daten wie der Geräte-Manager, ohne Adminrechte.
/// Seriennummern stecken bei manchen USB-Geräten in der Geräte-ID; die ID wird deshalb nur angezeigt, nie geloggt.
/// </summary>
public sealed class WindowsDeviceSource : IDeviceSource
{
    private readonly ILogger<WindowsDeviceSource> _logger;

    public WindowsDeviceSource(ILogger<WindowsDeviceSource> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DeviceInfo>? ReadDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return Wmi.Query("SELECT Name, PNPClass, PNPDeviceID, ConfigManagerErrorCode, Present FROM Win32_PnPEntity")
                // Geräte, die gerade nicht angeschlossen sind (z. B. ein abgezogener USB-Stick), zählen nicht
                .Where(row => row["Present"] is not false)
                .Select(row => new DeviceInfo(
                    (row["Name"] as string)?.Trim() ?? "?",
                    row["PNPClass"] as string,
                    row["PNPDeviceID"] as string ?? "?",
                    (int)(Wmi.ToLong(row["ConfigManagerErrorCode"]) ?? 0)))
                .ToList();
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "Geräteliste konnte nicht gelesen werden");
            return null;
        }
    }

    public IReadOnlyList<DriverInfo> ReadDrivers()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            return Wmi.Query("SELECT DeviceID, DeviceClass, DeviceName, DriverProviderName, DriverVersion, DriverDate, InfName FROM Win32_PnPSignedDriver")
                .Where(row => row["DeviceID"] is string)
                .Select(row => new DriverInfo(
                    (string)row["DeviceID"]!,
                    row["DeviceClass"] as string,
                    (row["DeviceName"] as string)?.Trim(),
                    (row["DriverProviderName"] as string)?.Trim(),
                    row["DriverVersion"] as string,
                    ParseDate(row["DriverDate"] as string),
                    row["InfName"] as string))
                .ToList();
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "Treiberliste konnte nicht gelesen werden");
            return [];
        }
    }

    /// <summary>WMI liefert Datumswerte als Text, z. B. „20250709000000.******+000“. Gebraucht wird nur der Tag.</summary>
    internal static DateOnly? ParseDate(string? value) =>
        value is { Length: >= 8 } && DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
