using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using Microsoft.Win32.SafeHandles;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>Ergebnis der Prüfung eines Ordners, in den der Hilfsprozess mit Adminrechten schreibt.</summary>
internal enum FolderVerdict
{
    Trusted,
    NotADirectory,
    /// <summary>Verknüpfung (Junction/Symlink): Sie könnte auf einen beliebigen anderen Ordner zeigen.</summary>
    ReparsePoint,
    /// <summary>Gehört nicht den Administratoren oder dem System – jemand anderes hat ihn angelegt.</summary>
    ForeignOwner,
    /// <summary>Rechte werden vom Elternordner geerbt (oder fehlen ganz) statt fest gesetzt zu sein.</summary>
    NotProtected,
    /// <summary>Außer Administratoren und System darf noch jemand darin schreiben, löschen oder Rechte ändern.</summary>
    OthersCanWrite,
}

/// <summary>Die Rechte eines Ordners, vereinfacht: Besitzer, ob die Rechte fest gesetzt sind, und die Einträge (null = keine DACL).</summary>
internal sealed record FolderSecurity(string? OwnerSid, bool IsProtected, IReadOnlyList<FolderAccess>? Entries);

/// <summary>Ein Eintrag der Rechteliste. <see cref="IsSimple"/> = gewöhnlicher Erlaubt-/Verweigert-Eintrag.</summary>
internal sealed record FolderAccess(string? Sid, bool IsAllow, int Mask, bool IsSimple);

/// <summary>
/// Ordner, in die nur Administratoren und das System schreiben dürfen. Wird ein Ordner neu angelegt, bekommt er seine Rechte
/// gleich beim Anlegen (kein Moment mit offenen Rechten). Ist er schon da, wird er über ein Handle geprüft, das keiner
/// Verknüpfung folgt – und bei einem Befund wird abgebrochen, nie gelöscht oder „repariert“: Wer den Ordner angelegt hat,
/// könnte das Löschen auf einen anderen Ordner umlenken oder offene Handles behalten, die eine Rechteänderung überstehen.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ProtectedFolder
{
    /// <summary>Besitzer Administratoren; Rechte nicht geerbt; Vollzugriff für Administratoren und System, vererbt auf alles darin.</summary>
    internal const string Sddl = "O:BAG:BAD:P(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)";

    /// <summary>
    /// Rechte, mit denen man in einem Ordner etwas verändern kann: Dateien/Ordner anlegen, Attribute schreiben, Inhalt löschen,
    /// löschen, Rechte oder Besitzer ändern, GENERIC_WRITE, GENERIC_ALL.
    /// </summary>
    internal const int ChangeRights = 0x0002 | 0x0004 | 0x0010 | 0x0040 | 0x0100 | 0x10000 | 0x40000 | 0x80000 | 0x40000000 | 0x10000000;

    /// <summary>BUILTIN\Administratoren und SYSTEM (feste, weltweit gleiche Kennungen).</summary>
    internal const string AdministratorsSid = "S-1-5-32-544";
    internal const string LocalSystemSid = "S-1-5-18";

    /// <summary>Darf der Hilfsprozess diesem Ordner vertrauen? (reine Funktion, für Tests)</summary>
    internal static FolderVerdict Decide(FileAttributes attributes, FolderSecurity security)
    {
        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            return FolderVerdict.NotADirectory;
        }

        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return FolderVerdict.ReparsePoint;
        }

        if (!IsAdminOrSystem(security.OwnerSid))
        {
            return FolderVerdict.ForeignOwner;
        }

        // Keine DACL heißt „alle dürfen alles“
        if (!security.IsProtected || security.Entries is null)
        {
            return FolderVerdict.NotProtected;
        }

        foreach (var entry in security.Entries)
        {
            // Nur einfache Einträge erwartet; alles andere (z. B. bedingte Einträge) gilt als unbekannt und damit als unsicher
            if (!entry.IsSimple || (entry.IsAllow && !IsAdminOrSystem(entry.Sid) && (entry.Mask & ChangeRights) != 0))
            {
                return FolderVerdict.OthersCanWrite;
            }
        }

        return FolderVerdict.Trusted;
    }

    /// <summary>Übersetzt die Rechte von Windows in das einfache Abbild für <see cref="Decide"/>.</summary>
    internal static FolderSecurity ToFolderSecurity(RawSecurityDescriptor descriptor) => new(
        descriptor.Owner?.Value,
        descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected),
        descriptor.DiscretionaryAcl?.Cast<GenericAce>()
            .Select(ace => ace is CommonAce common
                ? new FolderAccess(common.SecurityIdentifier.Value, common.AceQualifier == AceQualifier.AccessAllowed, common.AccessMask, IsSimple: true)
                : new FolderAccess(null, IsAllow: true, 0, IsSimple: false))
            .ToList());

    /// <summary>
    /// Legt den Ordner mit festen Rechten an, falls er fehlt, und prüft ihn danach immer (auch den gerade angelegten).
    /// Der Elternordner muss selbst schon vertrauenswürdig sein (z. B. „C:\Program Files“).
    /// </summary>
    /// <returns><see cref="FolderVerdict.Trusted"/> oder der Grund, warum der Ordner nicht benutzt werden darf.</returns>
    public static FolderVerdict Ensure(string path)
    {
        var descriptor = new RawSecurityDescriptor(Sddl);
        var binary = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(binary, 0);
        var pinned = GCHandle.Alloc(binary, GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = pinned.AddrOfPinnedObject(),
            };
            // Atomar: Entweder entsteht der Ordner jetzt mit genau diesen Rechten, oder es gab ihn schon (dann zählt nur die Prüfung)
            if (!CreateDirectoryW(path, ref attributes) && Marshal.GetLastPInvokeError() is var error && error != ErrorAlreadyExists)
            {
                throw new Win32Exception(error);
            }
        }
        finally
        {
            pinned.Free();
        }

        return Inspect(path);
    }

    /// <summary>Prüft einen vorhandenen Ordner über ein Handle auf den Eintrag selbst (folgt keiner Verknüpfung).</summary>
    public static FolderVerdict Inspect(string path)
    {
        using var handle = CreateFileW(
            path, ReadControl | FileReadAttributes, FileShareRead | FileShareWrite, IntPtr.Zero,
            OpenExisting, FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var attributes = (FileAttributes)File.GetAttributes(handle);
        var error = GetSecurityInfo(handle, SeFileObject, OwnerSecurityInformation | DaclSecurityInformation,
            out _, out _, out _, out _, out var descriptor);
        if (error != 0)
        {
            throw new Win32Exception(error);
        }

        try
        {
            var bytes = new byte[GetSecurityDescriptorLength(descriptor)];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            return Decide(attributes, ToFolderSecurity(new RawSecurityDescriptor(bytes, 0)));
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    private static bool IsAdminOrSystem(string? sid) => sid is AdministratorsSid or LocalSystemSid;

    private const int ErrorAlreadyExists = 183;
    private const uint ReadControl = 0x00020000;
    private const uint FileReadAttributes = 0x0080;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int SeFileObject = 1;
    private const uint OwnerSecurityInformation = 0x1;
    private const uint DaclSecurityInformation = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryW(string path, ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFileW(
        string path, uint access, uint share, IntPtr securityAttributes, uint creation, uint flags, IntPtr template);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSecurityInfo(
        SafeFileHandle handle, int objectType, uint securityInformation,
        out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr securityDescriptor);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSecurityDescriptorLength(IntPtr securityDescriptor);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
