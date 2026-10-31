using Omnikit.Modules.Performance.Data;

namespace Omnikit.Modules.Performance.Monitoring;

/// <summary>
/// Entscheidet, welche Netzwerkadapter Einsteiger sehen. Virtuelle Adapter (VPN, Hyper-V-Switch, VirtualBox …)
/// und getrennte Adapter verwirren nur; die Expertenansicht zeigt sie trotzdem.
/// </summary>
public static class NetworkAdapterFilter
{
    // Bewusst "Virtual Ethernet" statt "Hyper-V": Der Netzwerkadapter IN einer Hyper-V-VM heißt
    // "Microsoft Hyper-V Network Adapter" und ist dort der echte Anschluss.
    private static readonly string[] VirtualMarkers =
    [
        "Virtual Ethernet", "VirtualBox Host-Only", "VMware Virtual", "TAP-", "WireGuard", "Wintun", "Tunnel",
        "Loopback", "WAN Miniport", "Npcap", "Kernel Debug", "Wi-Fi Direct Virtual", "Bluetooth",
    ];

    public static bool IsVirtual(string description) =>
        VirtualMarkers.Any(marker => description.Contains(marker, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Windows weiß selbst, ob hinter einem Adapter Hardware steckt (<c>HardwareInterface</c> in MSFT_NetAdapter).
    /// Das erkennt auch VPN-Adapter, deren Namen keiner kennt. Fehlt die Angabe (<c>null</c>), entscheidet der Gerätename.
    /// </summary>
    public static bool IsVirtual(string description, bool? isHardware) =>
        isHardware == false || IsVirtual(description);

    public static bool IsShownInSimpleView(NetworkSample adapter) => adapter.IsConnected && !adapter.IsVirtual;

    /// <summary>
    /// .NET liefert unter Windows auch versteckte Schnittstellen, z. B. Filter-Schichten („…-WFP … Filter-0000“) desselben
    /// Adapters mit demselben Datenverkehr. Sichtbar sind nur Adapter, die Windows selbst anzeigt (wie <c>Get-NetAdapter</c>).
    /// Ist diese Liste nicht verfügbar (<paramref name="visibleAdapters"/> = null), wird nichts ausgefiltert.
    /// </summary>
    public static bool IsRealAdapter(string id, IReadOnlyDictionary<string, bool?>? visibleAdapters) =>
        visibleAdapters is null || visibleAdapters.ContainsKey(id);
}
