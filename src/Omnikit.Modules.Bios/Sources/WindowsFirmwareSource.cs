using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Omnikit.Core.Windows;
using Omnikit.Modules.Bios.Data;

namespace Omnikit.Modules.Bios.Sources;

/// <summary>
/// Liest BIOS-Version (WMI <c>Win32_BIOS</c>), Hauptplatine (<c>Win32_BaseBoard</c>), Startmodus (Windows-Funktion
/// <c>GetFirmwareType</c>) und Secure Boot (Registry). Alles ohne Adminrechte. Seriennummern werden nicht abgefragt.
/// </summary>
public sealed class WindowsFirmwareSource : IFirmwareSource
{
    private const string SecureBootKey = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";

    // Werte von GetFirmwareType
    private const int FirmwareTypeBios = 1;
    private const int FirmwareTypeUefi = 2;

    private readonly ILogger<WindowsFirmwareSource> _logger;

    public WindowsFirmwareSource(ILogger<WindowsFirmwareSource> logger)
    {
        _logger = logger;
    }

    public FirmwareInfo? Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var bios = Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS").FirstOrDefault();
            var board = Wmi.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
            var mode = ReadMode();

            return new FirmwareInfo(
                Clean(bios?["Manufacturer"]),
                Clean(bios?["SMBIOSBIOSVersion"]),
                ParseDate(bios?["ReleaseDate"] as string),
                mode,
                mode == FirmwareMode.Uefi ? ReadSecureBoot() : null,
                Clean(board?["Manufacturer"]),
                Clean(board?["Product"]));
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "BIOS-Daten konnten nicht gelesen werden");
            return null;
        }
    }

    /// <summary>WMI liefert Datumswerte als Text, z. B. „20240315000000.000000+000“. Gebraucht wird nur der Tag.</summary>
    internal static DateOnly? ParseDate(string? value) =>
        value is { Length: >= 8 } && DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static string? Clean(object? value) => value is string text && text.Trim().Length > 0 ? text.Trim() : null;

    private static FirmwareMode ReadMode() =>
        GetFirmwareType(out var type) ? type switch
        {
            FirmwareTypeBios => FirmwareMode.Legacy,
            FirmwareTypeUefi => FirmwareMode.Uefi,
            _ => FirmwareMode.Unknown,
        } : FirmwareMode.Unknown;

    private bool? ReadSecureBoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(SecureBootKey);
            return key?.GetValue("UEFISecureBootEnabled") is int value ? value == 1 : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Secure-Boot-Status konnte nicht gelesen werden");
            return null;
        }
    }

    // Windows-Funktion aus kernel32.dll: BIOS oder UEFI (seit Windows 8)
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(out int firmwareType);
}
