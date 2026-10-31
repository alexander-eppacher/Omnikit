namespace Omnikit.Core.SystemInfo;

/// <summary>
/// Für Nicht-Windows-Systeme (z. B. Linux beim Entwickeln): Windows-Werte sind "nicht verfügbar".
/// </summary>
public sealed class UnavailableSystemInfoProvider : ISystemInfoProvider
{
    public RawSystemInfo Read() => new()
    {
        IsAdmin = Environment.IsPrivilegedProcess,
        IsWindows = false,
    };
}
