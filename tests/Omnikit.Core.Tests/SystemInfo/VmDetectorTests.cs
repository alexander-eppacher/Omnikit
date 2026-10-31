using Omnikit.Core.SystemInfo;

namespace Omnikit.Core.Tests.SystemInfo;

public class VmDetectorTests
{
    // Echte Beispielwerte aus Win32_ComputerSystem (Manufacturer, Model).
    [Theory]
    [InlineData("Microsoft Corporation", "Virtual Machine", VmType.HyperV)]
    [InlineData("VMware, Inc.", "VMware7,1", VmType.VMware)]
    [InlineData("VMware, Inc.", "VMware Virtual Platform", VmType.VMware)]
    [InlineData("innotek GmbH", "VirtualBox", VmType.VirtualBox)]
    [InlineData("Oracle Corporation", "VirtualBox", VmType.VirtualBox)]
    [InlineData("QEMU", "Standard PC (Q35 + ICH9, 2009)", VmType.Qemu)]
    [InlineData("QEMU", "Standard PC (i440FX + PIIX, 1996)", VmType.Qemu)]
    [InlineData("Red Hat", "KVM", VmType.Qemu)]
    public void DetectsVirtualMachines(string manufacturer, string model, VmType expected)
    {
        Assert.Equal(expected, VmDetector.Detect(manufacturer, model));
    }

    [Theory]
    [InlineData("Dell Inc.", "OptiPlex 7090")]
    [InlineData("LENOVO", "20XW0026GE")]
    [InlineData("HP", "HP EliteBook 840 G8 Notebook PC")]
    [InlineData("Microsoft Corporation", "Surface Laptop 5")]
    [InlineData("Micro-Star International Co., Ltd.", "MS-7C02")]
    public void RealHardwareIsNotAVm(string manufacturer, string model)
    {
        Assert.Equal(VmType.None, VmDetector.Detect(manufacturer, model));
    }

    [Fact]
    public void ReturnsNullWithoutData()
    {
        Assert.Null(VmDetector.Detect(null, null));
    }

    [Fact]
    public void WorksWithOnlyOneValue()
    {
        Assert.Equal(VmType.VirtualBox, VmDetector.Detect(null, "VirtualBox"));
        Assert.Equal(VmType.Qemu, VmDetector.Detect("QEMU", null));
    }

    [Fact]
    public void IgnoresCase()
    {
        Assert.Equal(VmType.VMware, VmDetector.Detect("vmware, inc.", "vmware20,1"));
    }
}
