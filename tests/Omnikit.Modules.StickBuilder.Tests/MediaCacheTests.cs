using System.Security.Cryptography;
using Omnikit.Modules.StickBuilder.Creation;

namespace Omnikit.Modules.StickBuilder.Tests;

public sealed class MediaCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "omnikit-cache-" + Guid.NewGuid().ToString("N"));

    public MediaCacheTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Gibt beim ersten Lesen andere Daten heraus als danach – wie eine Datei, die während der Prüfung ausgetauscht wird.</summary>
    private sealed class SwappingStream(byte[] first, byte[] later) : Stream
    {
        private MemoryStream _current = new(first);
        private bool _swapped;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _current.Read(buffer, offset, Math.Min(count, 1000));
            if (!_swapped)
            {
                _swapped = true;
                _current = new MemoryStream(later) { Position = read };
            }

            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

#pragma warning disable CA5350 // Test der SHA-1 aus Microsofts Katalog
    private static string Sha1(byte[] data) => Convert.ToHexStringLower(SHA1.HashData(data));
#pragma warning restore CA5350

    [Fact]
    public async Task HashIsComputedFromTheBytesThatWereWritten()
    {
        var original = new byte[5000];
        var swapped = new byte[5000];
        new Random(1).NextBytes(original);
        new Random(2).NextBytes(swapped);
        using var output = new MemoryStream();

        var hash = await WindowsMediaBuilder.CopyWithHashAsync(new SwappingStream(original, swapped), output, TestContext.Current.CancellationToken);

        // Was in der Kopie steht, ist genau das, was geprüft wurde – egal, was die Quelle unterwegs gemacht hat
        Assert.Equal(Sha1(output.ToArray()), hash);
        Assert.NotEqual(Sha1(original), hash);
    }

    [Fact]
    public async Task CopyNeverOverwritesAnExistingFile()
    {
        var source = Path.Combine(_folder, "windows.esd");
        var target = Path.Combine(_folder, "kopie.tmp");
        await File.WriteAllTextAsync(source, "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(target, "schon da", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() => WindowsMediaBuilder.CopyWithHashAsync(source, target, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("aabbccddeeff00112233445566778899aabbccdd", true)]
    [InlineData("AABBCCDDEEFF00112233445566778899AABBCCDD", false)]   // nur Kleinbuchstaben (so kommt sie aus der Prüfung)
    [InlineData(@"..\..\..\Windows\System32\aabbccddeeff00112", false)]
    [InlineData(@"C:\aabbccddeeff00112233445566778899aabbc", false)]
    [InlineData("aabbccddeeff00112233445566778899aabbccd", false)]    // 39
    [InlineData("aabbccddeeff00112233445566778899aabbccdde", false)]  // 41
    public void OnlyRealChecksumsBecomeFolderNames(string value, bool valid)
    {
        Assert.Equal(valid, WindowsMediaBuilder.IsSha1(value));
    }

    [Fact]
    public async Task FolderCountsAsDoneOnlyWithTheRightChecksumInside()
    {
        const string sha1 = "aabbccddeeff00112233445566778899aabbccdd";
        var media = Path.Combine(_folder, sha1);
        Directory.CreateDirectory(media);
        Assert.False(WindowsMediaBuilder.IsDone(media, sha1));

        await File.WriteAllTextAsync(Path.Combine(media, WindowsMediaBuilder.DoneMarker), "ffffccddeeff00112233445566778899aabbccdd", TestContext.Current.CancellationToken);
        Assert.False(WindowsMediaBuilder.IsDone(media, sha1));

        await File.WriteAllTextAsync(Path.Combine(media, WindowsMediaBuilder.DoneMarker), sha1, TestContext.Current.CancellationToken);
        Assert.True(WindowsMediaBuilder.IsDone(media, sha1));
    }
}
