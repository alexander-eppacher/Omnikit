using Microsoft.Extensions.Logging.Abstractions;
using PcTool.Core.Settings;

namespace PcTool.Core.Tests.Settings;

/// <summary>
/// Jeder Test arbeitet in einem eigenen, temporären Ordner, der danach gelöscht wird.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "PcToolTests", Guid.NewGuid().ToString());

    private string SettingsPath => Path.Combine(_tempDirectory, "sub", "settings.json");

    private JsonSettingsStore CreateStore() => new(SettingsPath, NullLogger<JsonSettingsStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileIsMissing()
    {
        Assert.Equal(new AppSettings(), CreateStore().Load());
    }

    [Fact]
    public void SaveCreatesFolderAndLoadReadsItBack()
    {
        var store = CreateStore();
        var settings = new AppSettings { SchemaVersion = 7 };

        store.Save(settings);

        Assert.True(File.Exists(SettingsPath));
        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void ViewModeIsSavedAsTextAndReadBack()
    {
        var store = CreateStore();

        store.Save(new AppSettings { ViewMode = ViewMode.Expert });

        Assert.Contains("\"Expert\"", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
        Assert.Equal(ViewMode.Expert, store.Load().ViewMode);
    }

    [Fact]
    public void OldFileWithoutViewModeStartsInSimpleView()
    {
        // So sah die Datei vor Zwischenschritt 1.5 aus.
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "SchemaVersion": 1 }""");

        Assert.Equal(ViewMode.Simple, CreateStore().Load().ViewMode);
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileIsBroken()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ das ist kein gültiges JSON");

        Assert.Equal(new AppSettings(), CreateStore().Load());
    }

    [Fact]
    public void SaveLeavesNoTempFile()
    {
        CreateStore().Save(new AppSettings());

        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }
}
