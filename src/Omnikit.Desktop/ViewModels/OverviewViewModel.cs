using System.Globalization;
using Omnikit.Core.SystemInfo;
using Omnikit.Desktop.Resources;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Seite "Übersicht": zeigt die wichtigsten Systeminfos als lesbare Texte.
/// </summary>
public sealed class OverviewViewModel : ViewModelBase
{
    public OverviewViewModel(SystemContext context)
    {
        ShowNotWindowsHint = !context.IsWindows;
        Rows =
        [
            new InfoRow(Strings.Overview_OperatingSystem, OrUnavailable(context.OsName)),
            new InfoRow(Strings.Overview_Version, FormatVersion(context.OsDisplayVersion, context.OsBuild)),
            new InfoRow(Strings.Overview_Manufacturer, OrUnavailable(context.Manufacturer)),
            new InfoRow(Strings.Overview_Model, OrUnavailable(context.Model)),
            new InfoRow(Strings.Overview_SerialNumber, OrUnavailable(context.SerialNumber), Strings.Overview_SerialNumberNote),
            new InfoRow(Strings.Overview_VirtualMachine, FormatVmType(context.VmType)),
            new InfoRow(Strings.Overview_Admin, FormatYesNo(context.IsAdmin)),
            new InfoRow(Strings.Overview_AdminAccount, FormatYesNo(context.IsAdminAccount), Strings.Overview_AdminAccountNote),
            new InfoRow(Strings.Overview_Laptop, FormatYesNo(context.IsLaptop)),
        ];
    }

    public bool ShowNotWindowsHint { get; }

    public IReadOnlyList<InfoRow> Rows { get; }

    private static string OrUnavailable(string? value) => value ?? Strings.Value_Unavailable;

    /// <summary>z. B. "24H2 (Build 26100.2314)".</summary>
    private static string FormatVersion(string? displayVersion, string? build)
    {
        var buildText = build is null ? null : string.Format(CultureInfo.CurrentCulture, Strings.Value_Build, build);

        return (displayVersion, buildText) switch
        {
            (null, null) => Strings.Value_Unavailable,
            (not null, null) => displayVersion,
            (null, not null) => buildText,
            _ => $"{displayVersion} ({buildText})",
        };
    }

    private static string FormatYesNo(bool? value) => value switch
    {
        true => Strings.Value_Yes,
        false => Strings.Value_No,
        null => Strings.Value_Unavailable,
    };

    /// <summary>z. B. "Nein" oder "Ja (Hyper-V)".</summary>
    private static string FormatVmType(VmType? vmType) => vmType switch
    {
        null => Strings.Value_Unavailable,
        VmType.None => Strings.Value_No,
        VmType.HyperV => YesWith(Strings.VmType_HyperV),
        VmType.VMware => YesWith(Strings.VmType_VMware),
        VmType.VirtualBox => YesWith(Strings.VmType_VirtualBox),
        VmType.Qemu => YesWith(Strings.VmType_Qemu),
        _ => YesWith(vmType.Value.ToString()),
    };

    private static string YesWith(string detail) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Value_YesWithDetail, detail);
}
