using System.Net;

namespace Omnikit.Server;

/// <summary>Netze, aus denen ein Reverse Proxy kommen darf: die privaten Adressbereiche (RFC 1918 und IPv6 ULA).</summary>
public static class ProxyNetworks
{
    public static IReadOnlyList<System.Net.IPNetwork> Private { get; } =
    [
        new(new IPAddress([10, 0, 0, 0]), 8),
        new(new IPAddress([172, 16, 0, 0]), 12),
        new(new IPAddress([192, 168, 0, 0]), 16),
        System.Net.IPNetwork.Parse("fc00::/7"),
    ];
}
