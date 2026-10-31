using PcTool.Core.Modules;
using PcTool.Modules.Health.Data;
using PcTool.Modules.Health.Resources;
using static PcTool.Modules.Health.Evaluation.TextFormat;

namespace PcTool.Modules.Health.Evaluation;

/// <summary>
/// Bewertet ein Laufwerk: Zustand laut Windows, Restlebensdauer, Temperatur, Fehler.
/// </summary>
public static class DiskEvaluator
{
    // Grenzwerte (siehe Plan Phase 1)
    public const int RemainingLifeWarningBelow = 30;
    public const int RemainingLifeCriticalBelow = 10;
    public const int TemperatureWarningAbove = 60;
    public const int TemperatureCriticalAbove = 70;

    public static IReadOnlyList<Finding> Evaluate(DiskInfo disk, DiskReliability? reliability)
    {
        var category = HealthTexts.Category_Disks;
        var findings = new List<Finding> { EvaluateHealthStatus(disk, category) };

        if (reliability is null)
        {
            return findings;
        }

        // Verschleiß gibt es nur bei SSDs; bei Festplatten (HDD) ist der Wert bedeutungslos.
        if (reliability.WearPercent is int wear && disk.MediaType != DiskMediaType.Hdd)
        {
            var remaining = RemainingLife(wear);
            var severity = RateRemainingLife(remaining);
            var text = severity switch
            {
                Severity.Critical => HealthTexts.Disk_Wear_Critical,
                Severity.Warning => HealthTexts.Disk_Wear_Warning,
                _ => HealthTexts.Disk_Wear_Ok,
            };
            findings.Add(new Finding(
                Format(HealthTexts.Disk_Wear_Title, disk.Name), Format(text, remaining), severity, category,
                Details: Format(HealthTexts.Details_Wear, wear)));
        }

        // 0 °C bedeutet bei vielen Laufwerken "kein Sensor", nicht "eiskalt".
        if (reliability.TemperatureCelsius is int temperature and > 0)
        {
            var severity = RateTemperature(temperature);
            var text = severity switch
            {
                Severity.Critical => HealthTexts.Disk_Temp_Critical,
                Severity.Warning => HealthTexts.Disk_Temp_Warning,
                _ => HealthTexts.Disk_Temp_Ok,
            };
            findings.Add(new Finding(
                Format(HealthTexts.Disk_Temp_Title, disk.Name), Format(text, temperature), severity, category,
                Details: Details(
                    Format(HealthTexts.Details_TempNow, temperature),
                    FormatIf(HealthTexts.Details_TempMax, reliability.TemperatureMaxCelsius is > 0 ? reliability.TemperatureMaxCelsius : null))));
        }

        if (reliability.ReadErrorsTotal is not null || reliability.WriteErrorsTotal is not null)
        {
            var errors = (reliability.ReadErrorsTotal ?? 0) + (reliability.WriteErrorsTotal ?? 0);
            var details = Details(
                FormatIf(HealthTexts.Details_ReadErrors, reliability.ReadErrorsTotal),
                FormatIf(HealthTexts.Details_WriteErrors, reliability.WriteErrorsTotal));
            findings.Add(errors == 0
                ? new Finding(Format(HealthTexts.Disk_Errors_Title, disk.Name), HealthTexts.Disk_Errors_None, Severity.Ok, category, details)
                : new Finding(Format(HealthTexts.Disk_Errors_Title, disk.Name), Format(HealthTexts.Disk_Errors_Some, errors), Severity.Warning, category, details));
        }

        // Betriebsstunden sagen Einsteigern wenig und werden nicht bewertet: nur in der Expertenansicht.
        if (reliability.PowerOnHours is long hours)
        {
            findings.Add(new Finding(
                Format(HealthTexts.Disk_PowerOn_Title, disk.Name), Format(HealthTexts.Disk_PowerOn_Text, hours, hours / 24), Severity.Ok, category,
                IsExpertOnly: true));
        }

        return findings;
    }

    /// <summary>Restlebensdauer in Prozent. Verschleiß kann über 100 % liegen, die Restlebensdauer nicht unter 0.</summary>
    public static int RemainingLife(int wearPercent) => Math.Clamp(100 - wearPercent, 0, 100);

    public static Severity RateRemainingLife(int remainingPercent) => remainingPercent switch
    {
        < RemainingLifeCriticalBelow => Severity.Critical,
        < RemainingLifeWarningBelow => Severity.Warning,
        _ => Severity.Ok,
    };

    public static Severity RateTemperature(int celsius) => celsius switch
    {
        > TemperatureCriticalAbove => Severity.Critical,
        > TemperatureWarningAbove => Severity.Warning,
        _ => Severity.Ok,
    };

    private static Finding EvaluateHealthStatus(DiskInfo disk, string category)
    {
        var title = Format(HealthTexts.Disk_Health_Title, disk.Name);

        var (text, severity) = disk.HealthStatus switch
        {
            DiskHealthStatus.Healthy => (HealthTexts.Disk_Health_Healthy, Severity.Ok),
            DiskHealthStatus.Warning => (HealthTexts.Disk_Health_Warning, Severity.Warning),
            DiskHealthStatus.Unhealthy => (HealthTexts.Disk_Health_Unhealthy, Severity.Critical),
            _ => (HealthTexts.Disk_Health_Unknown, Severity.Info),
        };

        return new Finding(title, text, severity, category, DescribeDisk(disk));
    }

    /// <summary>Steckbrief des Laufwerks für die Expertenansicht, z. B. "Anschluss: NVMe · Typ: SSD · Größe: 953,9 GB · Firmware: 1.0".</summary>
    private static string? DescribeDisk(DiskInfo disk)
    {
        var type = disk.MediaType switch
        {
            DiskMediaType.Ssd => HealthTexts.Details_Type_Ssd,
            DiskMediaType.Hdd => HealthTexts.Details_Type_Hdd,
            _ => null,
        };

        return Details(
            FormatIf(HealthTexts.Details_Bus, disk.BusType),
            FormatIf(HealthTexts.Details_Type, type),
            FormatIf(HealthTexts.Details_Size, disk.SizeBytes is long size ? Bytes(size) : null),
            FormatIf(HealthTexts.Details_Firmware, disk.FirmwareVersion));
    }
}
