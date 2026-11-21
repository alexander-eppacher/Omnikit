using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Elevation;
using Omnikit.Core.Updates;
using Omnikit.Core.Windows;
using Omnikit.Operations.Resources;

namespace Omnikit.Operations;

/// <summary>
/// Installiert ein Update der App (nur in der installierten Version unter „Programme“). Die App hat Manifest, Signatur und Setup
/// ohne Adminrechte geladen – also an einem Ort, den jedes Programm des Benutzers ändern kann. Deshalb prüft diese Aktion alles
/// selbst noch einmal: Signatur über die Bytes, die sie selbst gelesen hat, Produkt, Version, und Größe und Prüfsumme der
/// Kopie im geschützten Ordner. Gestartet wird nur diese Kopie.
/// </summary>
public sealed class InstallUpdateOperation(ILogger<InstallUpdateOperation> logger) : IElevatedOperation
{
    public const string OperationId = "app.install-update";

    /// <summary>Geschützter Ordner für Setups (nur Administratoren und System dürfen schreiben).</summary>
    public static string UpdateFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Omnikit Updates");

    /// <summary>Ordner der installierten App.</summary>
    public static string InstallFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Omnikit");

    public string Id => OperationId;

    public TimeSpan Timeout => TimeSpan.FromMinutes(5);

    public static IReadOnlyList<string> Arguments(string manifest, string signature, string setup) => [manifest, signature, setup];

    public async Task<string> RunAsync(IReadOnlyList<string> arguments, IProgress<string> progress, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ElevatedOperationException(OperationTexts.Update_WindowsOnly);
        }

        var (manifestPath, signaturePath, setupPath) = ParseArguments(arguments);
        return await InstallAsync(manifestPath, signaturePath, setupPath, cancellationToken);
    }

    /// <summary>Drei vorhandene, absolute Pfade; Manifest und Signatur klein genug.</summary>
    internal static (string Manifest, string Signature, string Setup) ParseArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 3 || arguments.Any(path => !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !File.Exists(path))
            || new FileInfo(arguments[0]).Length > UpdateVerifier.MaxManifestBytes || new FileInfo(arguments[1]).Length != 64
            || new FileInfo(arguments[2]).Length > UpdateVerifier.MaxSetupBytes)
        {
            throw new ElevatedOperationException(OperationTexts.Update_BadArguments);
        }

        return (arguments[0], arguments[1], arguments[2]);
    }

    [SupportedOSPlatform("windows")]
    private async Task<string> InstallAsync(string manifestPath, string signaturePath, string setupPath, CancellationToken cancellationToken)
    {
        // Nur die installierte Version aktualisiert sich selbst; eine portable Kopie zeigt nur den Hinweis mit der Release-Seite
        if (Environment.ProcessPath is not { } self || !self.StartsWith(InstallFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ElevatedOperationException(OperationTexts.Update_NotInstalled);
        }

        // Signatur über genau die Bytes, die hier gelesen wurden (nicht die Datei, die sich danach noch ändern könnte)
        var manifestBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
        var signature = await File.ReadAllBytesAsync(signaturePath, cancellationToken);
        var installed = UpdateVerifier.InstalledVersion(typeof(InstallUpdateOperation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        var verdict = UpdateVerifier.Verify(manifestBytes, signature, UpdateSource.TrustedKeys, UpdateProduct.App, installed, out var manifest);
        if (verdict != UpdateVerdict.Valid)
        {
            logger.LogWarning("Update abgelehnt: {Verdict}", verdict);
            throw new ElevatedOperationException(OperationTexts.Update_NotGenuine);
        }

        if (ProtectedFolder.Ensure(UpdateFolder) is var folder && folder != FolderVerdict.Trusted)
        {
            logger.LogError("Update-Ordner {Folder} ist nicht geschützt: {Verdict}", UpdateFolder, folder);
            throw new ElevatedOperationException(OperationTexts.Update_FolderUntrusted);
        }

        // In den geschützten Ordner kopieren, dort prüfen, nur die Kopie starten
        var target = Path.Combine(UpdateFolder, manifest!.File);
        var partial = target + ".part";
        await using (var source = new FileStream(setupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var copy = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(copy, cancellationToken);
        }

        await using (var check = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (!await UpdateVerifier.MatchesAsync(check, manifest, cancellationToken))
            {
                File.Delete(partial);
                logger.LogWarning("Update abgelehnt: Kopie passt nicht zur Prüfsumme");
                throw new ElevatedOperationException(OperationTexts.Update_NotGenuine);
            }
        }

        File.Move(partial, target, overwrite: true);
        logger.LogInformation("Update {Version} geprüft, Setup wird gestartet", manifest.Version);

        // Nicht auf das Setup warten: Es ersetzt auch diese Datei und muss dafür den Hilfsprozess und die App beenden können
        var start = new ProcessStartInfo(target) { UseShellExecute = false, WorkingDirectory = UpdateFolder };
        foreach (var argument in UpdateSource.SilentSetupArguments(Path.Combine(UpdateFolder, "setup.log")))
        {
            start.ArgumentList.Add(argument);
        }

        using var _ = Process.Start(start) ?? throw new ElevatedOperationException(OperationTexts.Update_StartFailed);
        return manifest.Version;
    }
}
