using Omnikit.Core.SystemInfo;

namespace Omnikit.Core.Tests.Fakes;

/// <summary>
/// Liefert vorgegebene Rohdaten statt echter WMI-Werte.
/// </summary>
internal sealed class FakeSystemInfoProvider(RawSystemInfo rawInfo) : ISystemInfoProvider
{
    public RawSystemInfo Read() => rawInfo;
}
