using System.Text;
using Omnikit.Modules.Apps.Catalog;
using Omnikit.Modules.Apps.Winget;

namespace Omnikit.Modules.Apps.Tests.Catalog;

public class BuiltInCatalogTests
{
    [Fact]
    public void AllIdsAreValidAndUnique()
    {
        var ids = BuiltInCatalog.Apps.Select(app => app.PackageId).ToList();

        Assert.All(ids, id => Assert.True(PackageId.IsValid(id), id));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EveryAppHasNameAndDescription()
    {
        Assert.All(BuiltInCatalog.Apps, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(string.IsNullOrWhiteSpace(app.Description));
        });
    }

    [Fact]
    public void EveryCategoryHasAppsAndAName()
    {
        foreach (var category in Enum.GetValues<AppCategory>())
        {
            Assert.Contains(BuiltInCatalog.Apps, app => app.Category == category);
            Assert.False(string.IsNullOrWhiteSpace(BuiltInCatalog.CategoryName(category)));
        }
    }

    [Fact]
    public void TemplatesOnlyUseCatalogApps()
    {
        var ids = BuiltInCatalog.Apps.Select(app => app.PackageId).ToHashSet();

        Assert.Equal(3, BuiltInCatalog.Templates.Count);
        Assert.All(BuiltInCatalog.Templates, template => Assert.All(template.PackageIds, id => Assert.Contains(id, ids)));
    }
}

public class TemplateFileTests
{
    private static async Task<AppTemplate?> LoadAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await TemplateFile.LoadAsync(stream, "Meine Auswahl", CancellationToken.None);
    }

    [Fact]
    public async Task SavedTemplateCanBeLoadedAgain()
    {
        var template = new AppTemplate("Computerraum", ["Mozilla.Firefox", "7zip.7zip"]);
        using var stream = new MemoryStream();
        await TemplateFile.SaveAsync(stream, template, CancellationToken.None);
        stream.Position = 0;

        var loaded = await TemplateFile.LoadAsync(stream, "x", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Computerraum", loaded.Name);
        Assert.Equal(template.PackageIds, loaded.PackageIds);
    }

    [Fact]
    public async Task InvalidEntriesAreDropped()
    {
        var loaded = await LoadAsync("""{"SchemaVersion":1,"Name":"Test","Packages":["Mozilla.Firefox","--force","a b","x;calc","mozilla.firefox"]}""");

        Assert.NotNull(loaded);
        Assert.Equal(["Mozilla.Firefox"], loaded.PackageIds);
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("{}")]
    [InlineData("""{"SchemaVersion":2,"Packages":["Mozilla.Firefox"]}""")]
    [InlineData("""{"SchemaVersion":1,"Packages":[]}""")]
    [InlineData("""{"SchemaVersion":1,"Packages":["--force"]}""")]
    public async Task InvalidFilesAreRejected(string json)
    {
        Assert.Null(await LoadAsync(json));
    }

    [Fact]
    public async Task MissingNameUsesFallbackAndLongNamesAreShortened()
    {
        var withoutName = await LoadAsync("""{"SchemaVersion":1,"Packages":["7zip.7zip"]}""");
        var longName = await LoadAsync($$"""{"SchemaVersion":1,"Name":"{{new string('x', 300)}}","Packages":["7zip.7zip"]}""");

        Assert.Equal("Meine Auswahl", withoutName?.Name);
        Assert.Equal(80, longName?.Name.Length);
    }
}
