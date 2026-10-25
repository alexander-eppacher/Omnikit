using PcTool.Core.Modules;
using PcTool.Core.SystemInfo;
using PcTool.Core.Tests.Fakes;

namespace PcTool.Core.Tests.Modules;

public class ModuleRegistryTests
{
    [Fact]
    public void AllKeepsRegistrationOrder()
    {
        var registry = new ModuleRegistry([new FakeModule("health"), new FakeModule("apps")]);

        Assert.Equal(["health", "apps"], registry.All.Select(module => module.Id));
    }

    [Fact]
    public void GetSupportedSkipsUnsupportedModules()
    {
        var registry = new ModuleRegistry(
        [
            new FakeModule("always"),
            new FakeModule("laptop-only", context => context.IsLaptop == true),
        ]);
        var desktopPc = new SystemContext { IsLaptop = false };

        var supported = registry.GetSupported(desktopPc);

        Assert.Equal(["always"], supported.Select(module => module.Id));
    }

    [Fact]
    public void DuplicateIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new ModuleRegistry([new FakeModule("health"), new FakeModule("Health")]));
    }

    [Fact]
    public void EmptyRegistryIsAllowed()
    {
        var registry = new ModuleRegistry([]);

        Assert.Empty(registry.GetSupported(new SystemContext()));
    }
}
