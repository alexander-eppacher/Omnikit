namespace PcTool.Core.SystemInfo;

/// <summary>
/// Erkennt anhand von Hersteller und Modell (aus Win32_ComputerSystem), ob das Gerät eine VM ist.
/// Reine Funktion ohne Hardwarezugriff, deshalb gut testbar.
/// </summary>
public static class VmDetector
{
    /// <returns>Den VM-Typ, <see cref="VmType.None"/> für echte Rechner, oder <c>null</c>, wenn keine Daten vorliegen.</returns>
    public static VmType? Detect(string? manufacturer, string? model)
    {
        if (manufacturer is null && model is null)
        {
            return null;
        }

        var vendor = manufacturer ?? string.Empty;
        var product = model ?? string.Empty;

        // Hyper-V: Hersteller "Microsoft Corporation", Modell "Virtual Machine".
        // Nur der Hersteller reicht nicht, weil Surface-Geräte auch von Microsoft sind.
        if (Contains(vendor, "Microsoft") && Contains(product, "Virtual Machine"))
        {
            return VmType.HyperV;
        }

        // VMware: Hersteller "VMware, Inc.", Modell z. B. "VMware7,1" oder "VMware Virtual Platform".
        if (Contains(vendor, "VMware") || Contains(product, "VMware"))
        {
            return VmType.VMware;
        }

        // VirtualBox: Hersteller "innotek GmbH" (ältere Versionen) oder "Oracle Corporation", Modell "VirtualBox".
        if (Contains(vendor, "innotek") || Contains(product, "VirtualBox"))
        {
            return VmType.VirtualBox;
        }

        // QEMU/KVM und Proxmox: Hersteller "QEMU", Modell z. B. "Standard PC (Q35 + ICH9, 2009)".
        if (Contains(vendor, "QEMU") || Contains(product, "KVM") || product.StartsWith("Standard PC (", StringComparison.OrdinalIgnoreCase))
        {
            return VmType.Qemu;
        }

        return VmType.None;
    }

    private static bool Contains(string text, string part) =>
        text.Contains(part, StringComparison.OrdinalIgnoreCase);
}
