using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

using Omnikit.Agent.Resources;

namespace Omnikit.Agent;

/// <summary>Was der Agent sich merkt: Server-Adresse, eigener Token, optional das Stammzertifikat des Servers.</summary>
public sealed record AgentConfig(Uri Server, int DeviceId, bool HasCustomCa);

/// <summary>
/// Speichert Einstellungen und Token in <c>C:\Program Files\Omnikit Agent\config</c>. Unter „Programme“ können
/// normale Benutzer nichts anlegen (anders als in ProgramData, wo jemand den Ordner vorher anlegen und damit
/// Besitzer werden könnte). Der Ordner bekommt zusätzlich eigene Rechte: nur SYSTEM und Administratoren –
/// normale Benutzer können den Token weder lesen noch die Adresse ändern.
/// </summary>
public sealed class AgentStore(string directory)
{
    /// <summary>Hier liegt der installierte Agent; nur Administratoren dürfen dort schreiben.</summary>
    public static string InstallDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Omnikit Agent");

    public static AgentStore Default { get; } = new(Path.Combine(InstallDirectory, "config"));

    private string ConfigFile => Path.Combine(directory, "agent.json");

    private string TokenFile => Path.Combine(directory, "token");

    /// <summary>Stammzertifikat des Servers (z. B. von Caddys eigenem LAN-Zertifikat).</summary>
    public string CaFile => Path.Combine(directory, "server-ca.crt");

    /// <returns>Die Einstellungen oder <c>null</c>, wenn der Agent noch nicht registriert ist.</returns>
    /// <exception cref="InvalidDataException">Die Datei ist da, aber beschädigt. Das ist etwas anderes als „nicht registriert“.</exception>
    public AgentConfig? LoadConfig()
    {
        if (!File.Exists(ConfigFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(ConfigFile))
                is { Server: not null } config ? config : throw new InvalidDataException(AgentTexts.Config_Incomplete);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(AgentTexts.Config_Corrupt, ex);
        }
    }

    public string? LoadToken() => File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : null;

    public void Save(AgentConfig config, string token, string? caFile)
    {
        CreateProtectedDirectory();
        if (caFile is null)
        {
            File.Delete(CaFile); // kein eigenes Zertifikat (mehr); fehlt die Datei, passiert nichts
        }
        else if (!string.Equals(Path.GetFullPath(caFile), Path.GetFullPath(CaFile), StringComparison.OrdinalIgnoreCase))
        {
            // Zeigt --ca auf die schon gespeicherte Datei selbst, gibt es nichts zu kopieren
            File.Copy(caFile, CaFile, overwrite: true);
        }

        WriteAtomically(TokenFile, token);
        WriteAtomically(ConfigFile, JsonSerializer.Serialize(config));
    }

    /// <summary>Erst eine neue Datei schreiben, dann austauschen: Bricht etwas ab (Strom weg), bleibt die alte Datei ganz.</summary>
    private static void WriteAtomically(string path, string content)
    {
        var temporary = path + ".neu";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }

    public void Delete()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private void CreateProtectedDirectory()
    {
        var info = Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Vererbung kappen und nur SYSTEM + Administratoren Vollzugriff geben
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        }

        info.SetAccessControl(security);
    }
}
