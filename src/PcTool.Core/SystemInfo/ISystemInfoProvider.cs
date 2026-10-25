namespace PcTool.Core.SystemInfo;

/// <summary>
/// Liest die Rohdaten vom Betriebssystem. Hinter einem Interface, damit Tests ohne echte Hardware laufen.
/// </summary>
public interface ISystemInfoProvider
{
    RawSystemInfo Read();
}
