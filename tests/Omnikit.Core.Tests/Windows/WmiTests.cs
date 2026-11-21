using Omnikit.Core.Windows;

namespace Omnikit.Core.Tests;

public class WmiTests
{
    [Fact]
    public void NumbersOfAllWmiTypesAreConverted()
    {
        Assert.Equal(7L, Wmi.ToLong((ushort)7));
        Assert.Equal(32_000_000_000L, Wmi.ToLong(32_000_000_000UL));
        Assert.Equal(-1L, Wmi.ToLong(-1));
    }

    [Fact]
    public void UnusableValuesGiveNullInsteadOfAnError()
    {
        Assert.Null(Wmi.ToLong(null));
        Assert.Null(Wmi.ToLong("kein Wert"));
        Assert.Null(Wmi.ToLong(ulong.MaxValue));        // größer als long
        Assert.Null(Wmi.ToLong(DateTime.UnixEpoch));    // IConvertible, aber keine Zahl
        Assert.Null(Wmi.ToLong(new object()));
    }
}
