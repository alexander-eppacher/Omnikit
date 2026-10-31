using Microsoft.Extensions.Logging;
using PcTool.Modules.Health.Data;

namespace PcTool.Modules.Health.Sources;

/// <summary>
/// Freier Speicherplatz aller festen Laufwerke (C:\, D:\, ...). Funktioniert ohne Adminrechte und auch unter Linux.
/// </summary>
public sealed class VolumeSource : IVolumeSource
{
    private readonly ILogger<VolumeSource> _logger;

    public VolumeSource(ILogger<VolumeSource> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<VolumeInfo> ReadVolumes()
    {
        var volumes = new List<VolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady && drive.TotalSize > 0)
                {
                    volumes.Add(new VolumeInfo(drive.Name, drive.TotalSize, drive.AvailableFreeSpace, drive.DriveFormat));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Laufwerk {Drive} konnte nicht gelesen werden", drive.Name);
            }
        }

        return volumes;
    }
}
