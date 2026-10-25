using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcTool.Modules.Health.Data;

namespace PcTool.Modules.Health.Sources;

/// <summary>
/// Liest den Zustand von Windows über WMI und die Registry.
/// </summary>
public sealed class WindowsStateSource : IWindowsStateSource
{
    // Kennung von Windows selbst in SoftwareLicensingProduct (dort stehen auch Office-Lizenzen u. a.).
    private const string WindowsApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    // Gibt es einen dieser Registry-Schlüssel, wartet Windows auf einen Neustart.
    private static readonly string[] RebootPendingKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
    ];

    private readonly ILogger<WindowsStateSource> _logger;

    public WindowsStateSource(ILogger<WindowsStateSource> logger)
    {
        _logger = logger;
    }

    public WindowsState Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WindowsState(null, null, null);
        }

        return new WindowsState(ReadLastBootTime(), ReadRebootPending(), ReadIsActivated());
    }

    [SupportedOSPlatform("windows")]
    private DateTimeOffset? ReadLastBootTime()
    {
        try
        {
            var value = Wmi.Query("SELECT LastBootUpTime FROM Win32_OperatingSystem").FirstOrDefault()?["LastBootUpTime"] as string;
            return value is null ? null : new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(value));
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex) || ex is ArgumentException)
        {
            _logger.LogWarning(ex, "Letzter Neustart konnte nicht gelesen werden");
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private bool? ReadRebootPending()
    {
        try
        {
            return RebootPendingKeys.Any(path =>
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                return key is not null;
            });
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Ausstehender Neustart konnte nicht geprüft werden");
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private bool? ReadIsActivated()
    {
        try
        {
            // LicenseStatus 1 = lizenziert (aktiviert)
            var rows = Wmi.Query(
                $"SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID = '{WindowsApplicationId}' AND PartialProductKey IS NOT NULL");

            return rows.Count == 0 ? null : rows.Any(row => Wmi.ToLong(row["LicenseStatus"]) == 1);
        }
        catch (Exception ex) when (Wmi.IsExpectedError(ex))
        {
            _logger.LogWarning(ex, "Aktivierungsstatus konnte nicht gelesen werden");
            return null;
        }
    }
}
