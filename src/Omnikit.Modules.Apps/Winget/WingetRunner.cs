using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Omnikit.Modules.Apps.Winget;

/// <summary>
/// Startet winget als normaler Benutzer (nicht über den Admin-Hilfsprozess, siehe Entscheidung 0010).
/// Braucht ein Installer Adminrechte, fragt Windows selbst nach.
/// </summary>
public sealed class WingetRunner : IWingetRunner
{
    /// <summary>
    /// Fester Pfad statt Suche über PATH, damit keine gleichnamige fremde winget.exe gestartet wird.
    /// Dort legt Windows den Aufruf für den „App-Installer“ ab.
    /// </summary>
    private static string WingetPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");

    public async Task<WingetRunResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(WingetPath))
        {
            throw new WingetMissingException();
        }

        var startInfo = new ProcessStartInfo(WingetPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Argumente als Liste (Regel 6.1.4), nie als zusammengesetzter Text
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new WingetMissingException("winget konnte nicht gestartet werden.", ex);
        }

        if (process is null)
        {
            throw new WingetMissingException();
        }

        using (process)
        {
            // Eingabe sofort schließen: Fragt ein Paket trotz --disable-interactivity nach, bekommt es keine Antwort
            // und bricht ab, statt ewig zu warten.
            process.StandardInput.Close();

            // Ausgaben laufend mitlesen, sonst kann winget bei vollem Puffer hängen bleiben.
            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Nur winget selbst beenden, nicht einen gerade laufenden Installer (das könnte das Programm beschädigen).
                process.Kill(entireProcessTree: false);
                await process.WaitForExitAsync(CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
                return new WingetRunResult(process.ExitCode, await output + await errors, TimedOut: true);
            }

            return new WingetRunResult(process.ExitCode, await output + await errors);
        }
    }
}
