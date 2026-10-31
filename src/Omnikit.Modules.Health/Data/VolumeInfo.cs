namespace Omnikit.Modules.Health.Data;

/// <summary>
/// Eine Partition mit Laufwerksbuchstaben, z. B. "C:\".
/// </summary>
/// <param name="FileSystem">z. B. "NTFS" (nur Expertenansicht).</param>
public sealed record VolumeInfo(string Name, long TotalBytes, long FreeBytes, string? FileSystem = null);
