using Omnikit.Core.Elevation;
using Omnikit.Modules.StickBuilder.Creation;
using Omnikit.Modules.StickBuilder.Download;

namespace Omnikit.Modules.StickBuilder.Tests;

public class WindowsCatalogTests
{
    // Aufbau wie products.xml des Media Creation Tool (gekürzt, Werte ausgedacht)
    private const string Sample = """
        <MCT><Catalogs><Catalog><PublishedMedia><Files>
          <File><FileName>26100.1_CLIENTCONSUMER_RET_x64FRE_de-de.esd</FileName><LanguageCode>de-de</LanguageCode><Language>German (Germany)</Language>
            <Edition>Professional</Edition><Architecture>x64</Architecture><Size>4500000000</Size><Sha1>AABBCCDDEEFF00112233445566778899AABBCCDD</Sha1>
            <FilePath>http://dl.delivery.mp.microsoft.com/files/x/26100.1_CLIENTCONSUMER_RET_x64FRE_de-de.esd</FilePath></File>
          <File><FileName>26100.1_CLIENTCONSUMER_RET_x64FRE_de-de.esd</FileName><LanguageCode>de-de</LanguageCode><Language>German (Germany)</Language>
            <Edition>Core</Edition><Architecture>x64</Architecture><Size>4500000000</Size><Sha1>aabbccddeeff00112233445566778899aabbccdd</Sha1>
            <FilePath>http://dl.delivery.mp.microsoft.com/files/x/26100.1_CLIENTCONSUMER_RET_x64FRE_de-de.esd</FilePath></File>
          <File><FileName>26100.1_CLIENTCONSUMER_RET_A64FRE_de-de.esd</FileName><LanguageCode>de-de</LanguageCode><Language>German (Germany)</Language>
            <Edition>Professional</Edition><Architecture>ARM64</Architecture><Size>4500000000</Size><Sha1>aabbccddeeff00112233445566778899aabbccdd</Sha1>
            <FilePath>http://dl.delivery.mp.microsoft.com/files/y/a64.esd</FilePath></File>
          <File><FileName>26100.1_CLIENTBUSINESS_VOL_x64FRE_de-de.esd</FileName><LanguageCode>de-de</LanguageCode><Language>German (Germany)</Language>
            <Edition>Enterprise</Edition><Architecture>x64</Architecture><Size>4500000000</Size><Sha1>aabbccddeeff00112233445566778899aabbccdd</Sha1>
            <FilePath>http://dl.delivery.mp.microsoft.com/files/z/business.esd</FilePath></File>
          <File><FileName>26100.1_CLIENTCONSUMER_RET_x64FRE_it-it.esd</FileName><LanguageCode>it-it</LanguageCode><Language>Italian (Italy)</Language>
            <Edition>Professional</Edition><Architecture>x64</Architecture><Size>4400000000</Size><Sha1>aabbccddeeff00112233445566778899aabbccdd</Sha1>
            <FilePath>http://evil.example.com/it-it.esd</FilePath></File>
        </Files></PublishedMedia></Catalog></Catalogs></MCT>
        """;

    [Fact]
    public void OneConsumerFilePerLanguageFromMicrosoft()
    {
        var downloads = WindowsCatalog.Parse(Sample);

        var german = Assert.Single(downloads); // ARM, Business und fremde Server fallen weg
        Assert.Equal("de-de", german.LanguageCode);
        Assert.Equal("aabbccddeeff00112233445566778899aabbccdd", german.Sha1);
        Assert.Equal(4500000000, german.SizeBytes);
        Assert.Equal("dl.delivery.mp.microsoft.com", german.Uri.Host);
    }

    [Theory]
    [InlineData("http://dl.delivery.mp.microsoft.com/a.esd", true)]
    [InlineData("https://download.microsoft.com/a.cab", true)]
    [InlineData("http://microsoft.com.evil.example/a.esd", false)]
    [InlineData("http://evilmicrosoft.com/a.esd", false)]
    [InlineData("ftp://dl.delivery.mp.microsoft.com/a.esd", false)]
    public void OnlyMicrosoftServersAreTrusted(string url, bool trusted)
    {
        Assert.Equal(trusted, WindowsCatalog.IsMicrosoftHost(new Uri(url)));
    }
}

public sealed class EsdArgumentsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "omnikit-esd-" + Guid.NewGuid().ToString("N"));

    public EsdArgumentsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void EsdNeedsAValidChecksum()
    {
        var esd = Path.Combine(_folder, "windows.esd");
        File.WriteAllText(esd, "x");
        const string sha1 = "AABBCCDDEEFF00112233445566778899AABBCCDD";

        var parsed = CreateStickOperation.ParseArguments(["1", "32000000000", CreateStickTests.DevicePath, "0", esd, sha1]);

        Assert.Equal(sha1.ToLowerInvariant(), parsed.Sha1);
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["1", "32000000000", CreateStickTests.DevicePath, "0", esd]));            // ohne Prüfsumme
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["1", "32000000000", CreateStickTests.DevicePath, "0", esd, "abc"]));     // zu kurz
        Assert.Throws<ElevatedOperationException>(() => CreateStickOperation.ParseArguments(["1", "32000000000", CreateStickTests.DevicePath, "0", esd, new string('z', 40)]));
    }

    [Fact]
    public void DismIndexesAreRead()
    {
        const string output = "Details for image : x.esd\n\nIndex : 1\nName : Windows Setup Media\n\nIndex : 2\nName : Microsoft Windows PE (amd64)\n\nIndex : 4\nName : Windows 11 Home\n";

        Assert.Equal([1, 2, 4], Dism.ParseIndexes(output));
    }
}

public class EditionFilterTests
{
    [Theory]
    [InlineData("Details for image : x.esd\n\nIndex : 6\nName : Windows 11 Pro\nEdition : Professional\nInstallation : Client\n", "Professional")]
    [InlineData("Index : 4\nName : Windows 11 Home\nEdition : Core\n", "Core")]
    [InlineData("Index : 1\nName : Windows Setup Media\n", null)]
    public void EditionIsRead(string output, string? expected)
    {
        Assert.Equal(expected, WindowsMediaBuilder.ParseEdition(output));
    }

    [Fact]
    public void OnlyCommonEditionsAreKept()
    {
        Assert.Equal(["Core", "Professional", "Education", "ProfessionalEducation"], WindowsMediaBuilder.Editions);
    }
}
