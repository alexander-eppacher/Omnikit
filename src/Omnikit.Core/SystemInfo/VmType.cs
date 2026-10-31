namespace Omnikit.Core.SystemInfo;

/// <summary>
/// Art der virtuellen Maschine. <see cref="None"/> bedeutet: echter Rechner.
/// </summary>
public enum VmType
{
    None,
    HyperV,
    VMware,
    VirtualBox,
    /// <summary>QEMU/KVM, dazu gehört auch Proxmox.</summary>
    Qemu,
}
