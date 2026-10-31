using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Settings;

namespace Omnikit.Core.Tests.Settings;

/// <summary>
/// Jeder Test arbeitet in einem eigenen, temporären Ordner, der danach gelöscht wird.
/// </summary>
public sealed class LegacySettingsTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "OmnikitTests", Guid.NewGuid().ToString());

    private string LegacyPath => Path.Combine(_tempDirectory, "PcTool", "settings.json");

    private string NewPath => Path.Combine(_tempDirectory, "Omnikit", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void OldSettingsAreCopiedWhenNewFileIsMissing()
    {
        WriteFile(LegacyPath, "alt");

        LegacySettings.CopyIfMissing(LegacyPath, NewPath, NullLogger.Instance);

        Assert.Equal("alt", File.ReadAllText(NewPath));
        Assert.True(File.Exists(LegacyPath)); // nur kopiert, nicht gelöscht
    }

    [Fact]
    public void ExistingNewSettingsAreNeverOverwritten()
    {
        WriteFile(LegacyPath, "alt");
        WriteFile(NewPath, "neu");

        LegacySettings.CopyIfMissing(LegacyPath, NewPath, NullLogger.Instance);

        Assert.Equal("neu", File.ReadAllText(NewPath));
    }

    [Fact]
    public void NothingHappensWithoutOldSettings()
    {
        LegacySettings.CopyIfMissing(LegacyPath, NewPath, NullLogger.Instance);

        Assert.False(File.Exists(NewPath));
    }
}
