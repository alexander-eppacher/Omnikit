using System.Runtime.Versioning;
using Omnikit.Core.Windows;

namespace Omnikit.Core.Tests.Windows;

/// <summary>
/// Prüfregel für Ordner, in die der Hilfsprozess mit Adminrechten schreibt. Die Rechte werden als einfaches Abbild
/// zusammengesetzt, damit die Tests auch unter Linux laufen (die Rechte-Klassen von .NET gibt es nur unter Windows).
/// </summary>
[SupportedOSPlatform("windows")]
public class ProtectedFolderTests
{
    private const string Users = "S-1-5-32-545";
    private const string SomeUser = "S-1-5-21-1000-2000-3000-1001";
    private const int FullControl = 0x1F01FF;
    private const int ReadAndExecute = 0x1200A9;

    private static FolderSecurity Descriptor(string owner, bool isProtected, params (string Sid, int Mask, bool Allow)[] aces) =>
        new(owner, isProtected, aces.Select(ace => new FolderAccess(ace.Sid, ace.Allow, ace.Mask, IsSimple: true)).ToList());

    private static readonly (string, int, bool)[] Good =
    [
        (ProtectedFolder.AdministratorsSid, FullControl, true),
        (ProtectedFolder.LocalSystemSid, FullControl, true),
    ];

    [Fact]
    public void OwnFolderIsTrusted()
    {
        Assert.Equal(FolderVerdict.Trusted, ProtectedFolder.Decide(FileAttributes.Directory, Descriptor(ProtectedFolder.AdministratorsSid, true, Good)));
        Assert.Equal(FolderVerdict.Trusted, ProtectedFolder.Decide(FileAttributes.Directory, Descriptor(ProtectedFolder.LocalSystemSid, true, Good)));
    }

    [Fact]
    public void ReadingForOthersIsFine()
    {
        var security = Descriptor(ProtectedFolder.AdministratorsSid, true, [.. Good, (Users, ReadAndExecute, true)]);

        Assert.Equal(FolderVerdict.Trusted, ProtectedFolder.Decide(FileAttributes.Directory, security));
    }

    [Fact]
    public void FolderCreatedByAUserIsNotTrusted()
    {
        Assert.Equal(FolderVerdict.ForeignOwner, ProtectedFolder.Decide(FileAttributes.Directory, Descriptor(SomeUser, true, Good)));
    }

    [Fact]
    public void JunctionIsNeverTrusted()
    {
        // Auch wenn die Rechte am Eintrag selbst stimmen: Die Verknüpfung könnte überallhin zeigen
        var security = Descriptor(ProtectedFolder.AdministratorsSid, true, Good);

        Assert.Equal(FolderVerdict.ReparsePoint, ProtectedFolder.Decide(FileAttributes.Directory | FileAttributes.ReparsePoint, security));
    }

    [Fact]
    public void FileInsteadOfFolderIsNotTrusted()
    {
        Assert.Equal(FolderVerdict.NotADirectory, ProtectedFolder.Decide(FileAttributes.Normal, Descriptor(ProtectedFolder.AdministratorsSid, true, Good)));
    }

    [Fact]
    public void InheritedRightsAreNotTrusted()
    {
        Assert.Equal(FolderVerdict.NotProtected, ProtectedFolder.Decide(FileAttributes.Directory, Descriptor(ProtectedFolder.AdministratorsSid, false, Good)));
    }

    [Theory]
    [InlineData(0x0002)]        // Datei anlegen
    [InlineData(0x0004)]        // Unterordner anlegen
    [InlineData(0x0040)]        // Inhalt löschen
    [InlineData(0x10000)]       // löschen
    [InlineData(0x40000)]       // Rechte ändern
    [InlineData(0x80000)]       // Besitzer ändern
    [InlineData(FullControl)]
    public void OthersWithChangeRightsAreNotTrusted(int mask)
    {
        var security = Descriptor(ProtectedFolder.AdministratorsSid, true, [.. Good, (Users, mask, true)]);

        Assert.Equal(FolderVerdict.OthersCanWrite, ProtectedFolder.Decide(FileAttributes.Directory, security));
    }

    [Fact]
    public void DenyEntriesForOthersDoNoHarm()
    {
        var security = Descriptor(ProtectedFolder.AdministratorsSid, true, [.. Good, (Users, FullControl, false)]);

        Assert.Equal(FolderVerdict.Trusted, ProtectedFolder.Decide(FileAttributes.Directory, security));
    }

    [Fact]
    public void MissingAclOrUnknownEntriesAreNotTrusted()
    {
        Assert.Equal(FolderVerdict.NotProtected,
            ProtectedFolder.Decide(FileAttributes.Directory, new FolderSecurity(ProtectedFolder.AdministratorsSid, true, null)));
        Assert.Equal(FolderVerdict.OthersCanWrite, ProtectedFolder.Decide(FileAttributes.Directory, new FolderSecurity(
            ProtectedFolder.AdministratorsSid, true, [new FolderAccess(null, IsAllow: true, 0, IsSimple: false)])));
        Assert.Equal(FolderVerdict.ForeignOwner,
            ProtectedFolder.Decide(FileAttributes.Directory, new FolderSecurity(null, true, [])));
    }
}
