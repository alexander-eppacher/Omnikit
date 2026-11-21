using System.Globalization;
using Omnikit.Core.Modules;
using Omnikit.Modules.Bios.Data;
using Omnikit.Modules.Bios.Resources;

namespace Omnikit.Modules.Bios.Evaluation;

/// <summary>Macht aus den BIOS-Daten die Befunde für die Seite „BIOS“. Nur lesen und melden – nie flashen.</summary>
public static class FirmwareEvaluator
{
    /// <summary>Ab diesem Alter gibt es einen Hinweis, beim Hersteller nach einer neueren Version zu schauen.</summary>
    public const int OldAfterYears = 3;

    public static IReadOnlyList<Finding> Evaluate(FirmwareInfo firmware, SupportPage? support, bool isVm, DateOnly today)
    {
        var findings = new List<Finding>();
        var category = BiosTexts.Category_Firmware;

        var version = Format(BiosTexts.Version_Text, firmware.Version ?? "?", Date(firmware.ReleaseDate), firmware.Manufacturer ?? "?");
        findings.Add(new Finding(
            BiosTexts.Version_Title,
            isVm ? version + " " + BiosTexts.Version_Vm : version,
            Severity.Ok,
            category,
            Format(BiosTexts.Details_Board, firmware.BoardManufacturer ?? "?", firmware.BoardProduct ?? "?")));

        if (!isVm && firmware.ReleaseDate is DateOnly released && released.AddYears(OldAfterYears) < today)
        {
            findings.Add(new Finding(BiosTexts.Old_Title, Format(BiosTexts.Old_Text, Date(released)), Severity.Info, category));
        }

        findings.Add(firmware.Mode switch
        {
            FirmwareMode.Uefi => new Finding(BiosTexts.Mode_Title, BiosTexts.Mode_Uefi, Severity.Ok, category),
            FirmwareMode.Legacy => new Finding(BiosTexts.Mode_Title, isVm ? BiosTexts.Mode_LegacyVm : BiosTexts.Mode_Legacy, Severity.Info, category),
            _ => new Finding(BiosTexts.Mode_Title, BiosTexts.Mode_Unknown, Severity.Info, category),
        });

        // Secure Boot gibt es nur mit UEFI; beim alten BIOS sagt der Startmodus schon alles
        if (firmware.Mode == FirmwareMode.Uefi && firmware.SecureBootEnabled is bool secureBoot)
        {
            findings.Add(secureBoot
                ? new Finding(BiosTexts.SecureBoot_Title, BiosTexts.SecureBoot_On, Severity.Ok, category)
                : new Finding(BiosTexts.SecureBoot_Title, BiosTexts.SecureBoot_Off, isVm ? Severity.Info : Severity.Warning, category));
        }

        if (!isVm)
        {
            findings.Add(new Finding(
                BiosTexts.Updates_Title,
                support is null ? BiosTexts.Updates_Unknown : Format(BiosTexts.Updates_Known, support.Name),
                Severity.Ok,
                BiosTexts.Category_Updates,
                support?.Url.ToString()));
        }

        return findings;
    }

    private static string Date(DateOnly? date) => date?.ToString("d", CultureInfo.CurrentCulture) ?? "?";

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
