namespace PcTool.Modules.Health.Data;

/// <summary>
/// Eine Partition mit Laufwerksbuchstaben, z. B. "C:\".
/// </summary>
public sealed record VolumeInfo(string Name, long TotalBytes, long FreeBytes);
