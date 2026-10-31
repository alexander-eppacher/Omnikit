namespace Omnikit.Modules.Apps.Winget;

/// <summary>Ein installiertes Programm, das winget kennt.</summary>
/// <param name="Id">winget-Paket-ID, z. B. "7zip.7zip".</param>
/// <param name="Version">Installierte Version, wie winget sie meldet (kann z. B. "&gt; 1.8.10" sein).</param>
/// <param name="Source">Quelle, z. B. "winget" oder "msstore".</param>
public sealed record InstalledPackage(string Id, string Version, string Source);

/// <summary>Ein installiertes Programm, für das eine neuere Version bereitsteht.</summary>
public sealed record AvailableUpgrade(string Name, string Id, string InstalledVersion, string AvailableVersion, string Source);
