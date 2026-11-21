using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Omnikit.Modules.StickBuilder.Creation;

/// <summary>
/// Ruft DISM auf (in Windows enthalten, „Deployment Image Servicing and Management“) und liest den Fortschritt mit.
/// „/English“ sorgt für englische Ausgaben, damit sie unabhängig von der Windows-Sprache gelesen werden können.
/// </summary>
internal static partial class Dism
{
    /// <exception cref="InvalidOperationException">DISM meldet einen Fehler (Text fürs Log).</exception>
    public static async Task<string> RunAsync(IEnumerable<string> arguments, Action<double>? onPercent, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "dism.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("/English");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("DISM konnte nicht gestartet werden.");
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);

        // DISM zeichnet den Fortschrittsbalken mit "\r" in dieselbe Zeile: zeichenweise lesen
        var output = new StringBuilder();
        var line = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await process.StandardOutput.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (c is '\r' or '\n')
                {
                    if (onPercent is not null && PercentPattern().Match(line.ToString()) is { Success: true } match)
                    {
                        onPercent(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                    }

                    output.Append(line).Append('\n');
                    line.Clear();
                }
                else
                {
                    line.Append(c);
                }
            }
        }

        await process.WaitForExitAsync(cancellationToken);
        output.Append(line);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"DISM fehlgeschlagen ({process.ExitCode}): {LastLines(output.ToString())} {(await errors).Trim()}");
        }

        return output.ToString();
    }

    /// <summary>Liest „Index : 4“ aus der Ausgabe von /Get-WimInfo.</summary>
    public static IReadOnlyList<int> ParseIndexes(string getWimInfoOutput) =>
        IndexPattern().Matches(getWimInfoOutput).Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();

    private static string LastLines(string text) =>
        string.Join(" | ", text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).TakeLast(4));

    [GeneratedRegex(@"(\d{1,3}\.\d)%")]
    private static partial Regex PercentPattern();

    [GeneratedRegex(@"^Index\s*:\s*(\d+)\s*$", RegexOptions.Multiline)]
    private static partial Regex IndexPattern();
}
