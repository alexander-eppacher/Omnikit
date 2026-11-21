using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>
/// Führt kurze PowerShell-Befehle der Windows-Speicherverwaltung aus (Partitionieren, Formatieren, ISO einbinden).
/// Der Befehl wird als <c>-EncodedCommand</c> übergeben, nicht als Skriptdatei: Eine Datei in einem beschreibbaren Ordner
/// könnte ein anderes Programm austauschen, bevor sie mit Adminrechten läuft.
/// </summary>
internal static class PowerShellCommand
{
    /// <summary>Text sicher in einfache Anführungszeichen setzen (in PowerShell wird ' als '' geschrieben).</summary>
    public static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Fehler als lesbaren Text melden: Ohne das schreibt PowerShell Fortschritt und Fehler als XML („CLIXML“) in die Fehlerausgabe,
    /// und die eigentliche Meldung ist im Log nicht mehr zu finden.
    /// </summary>
    internal static string Wrap(string script) =>
        "$ErrorActionPreference = 'Stop'\n$ProgressPreference = 'SilentlyContinue'\n"
        + "try {\n" + script + "\n} catch {\n[Console]::Error.WriteLine($_.Exception.Message)\nexit 1\n}";

    /// <param name="environment">
    /// Werte, die das Skript als <c>$env:NAME</c> liest. So kommen Texte wie Gerätepfade ins Skript, ohne je Teil des Skripttexts zu sein.
    /// </param>
    /// <returns>Die Ausgabe des Befehls (ohne Leerzeilen am Ende).</returns>
    /// <exception cref="PowerShellException">Der Befehl ist fehlgeschlagen; Exit-Code und Meldung von PowerShell stehen in der Ausnahme (fürs Log).</exception>
    public static async Task<string> RunAsync(
        string script, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(Wrap(script))),
            },
        };
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo) ?? throw new Win32Exception("PowerShell konnte nicht gestartet werden.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new PowerShellException(process.ExitCode, (await errors).Trim());
        }

        return (await output).Trim();
    }
}

/// <summary>Ein PowerShell-Befehl ist mit einem Exit-Code ungleich 0 beendet worden.</summary>
internal sealed class PowerShellException(int exitCode, string errorOutput)
    : InvalidOperationException($"PowerShell-Befehl fehlgeschlagen ({exitCode}): {errorOutput}")
{
    public int ExitCode { get; } = exitCode;
}
