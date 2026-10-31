using Omnikit.Core.Modules;
using Omnikit.Modules.Health.Data;
using Omnikit.Modules.Health.Resources;
using static Omnikit.Modules.Health.Evaluation.TextFormat;

namespace Omnikit.Modules.Health.Evaluation;

/// <summary>
/// Bewertet den freien Speicherplatz einer Partition.
/// </summary>
public static class StorageEvaluator
{
    public const int FreeWarningBelowPercent = 15;
    public const int FreeCriticalBelowPercent = 5;
    public const long FreeCriticalBelowBytes = 5L * 1024 * 1024 * 1024; // 5 GB

    public static Finding Evaluate(VolumeInfo volume)
    {
        var freePercent = Percent(volume.FreeBytes, volume.TotalBytes);
        var severity = Rate(volume.FreeBytes, volume.TotalBytes);
        var text = severity switch
        {
            Severity.Critical => HealthTexts.Storage_Critical,
            Severity.Warning => HealthTexts.Storage_Warning,
            _ => HealthTexts.Storage_Ok,
        };

        return new Finding(
            Format(HealthTexts.Storage_Title, volume.Name),
            Format(text, Bytes(volume.FreeBytes), Bytes(volume.TotalBytes), freePercent),
            severity,
            HealthTexts.Category_Storage,
            FormatIf(HealthTexts.Details_FileSystem, volume.FileSystem));
    }

    /// <summary>Kritisch unter 5 % ODER unter 5 GB (auch eine große Platte mit 4 GB frei ist praktisch voll).</summary>
    public static Severity Rate(long freeBytes, long totalBytes)
    {
        var freePercent = totalBytes <= 0 ? 0 : 100.0 * freeBytes / totalBytes;

        if (freePercent < FreeCriticalBelowPercent || freeBytes < FreeCriticalBelowBytes)
        {
            return Severity.Critical;
        }

        return freePercent < FreeWarningBelowPercent ? Severity.Warning : Severity.Ok;
    }
}
