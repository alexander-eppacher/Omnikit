using System.IO.Compression;
using System.Text;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder;

/// <summary>
/// Alles für den Stick in einer ZIP-Datei: <c>autounattend.xml</c> (kommt ins Stammverzeichnis des Sticks),
/// die Skripte zum Nachlesen (sie stecken auch in der Antwortdatei) und eine Anleitung.
/// </summary>
public static class StickPackage
{
    public const string AnswerFileName = "autounattend.xml";
    public const string GuideFileName = "ANLEITUNG.txt";

    public static async Task WriteZipAsync(Stream stream, StickOptions options, CancellationToken cancellationToken)
    {
        // Erst alles erzeugen (wirft bei ungültigen Optionen), dann schreiben
        var answerFile = AnswerFile.Create(options);
        // Nur die Skripte zum Nachlesen; das WLAN-Profil (mit Passwort) steckt ohnehin in der Antwortdatei
        var scripts = AnswerFile.EmbeddedFiles(options).Where(file => file.Path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));

        await using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        await AddAsync(zip, AnswerFileName, answerFile, withBom: false, cancellationToken);
        await AddAsync(zip, GuideFileName, Guide(options), withBom: true, cancellationToken);
        foreach (var (path, content) in scripts)
        {
            await AddAsync(zip, "Skripte/" + Path.GetFileName(path.Replace('\\', '/')), content, withBom: true, cancellationToken);
        }
    }

    public static string SuggestFileName(DateTimeOffset now) => $"Omnikit-Stick-{now:yyyy-MM-dd}.zip";

    /// <summary>Anleitung als Text (Windows-Zeilenenden, damit sie auch im alten Editor lesbar ist).</summary>
    public static string Guide(StickOptions options)
    {
        var text = Texts.Guide_Text;
        if (options.Disk == DiskMode.WipeFirstDisk)
        {
            text += Environment.NewLine + Environment.NewLine + Texts.Guide_WipeWarning;
        }

        return text.ReplaceLineEndings("\r\n");
    }

    /// <param name="withBom">Skripte und Text mit BOM: Windows PowerShell 5.1 und ältere Editoren lesen Umlaute sonst falsch.
    /// Die Antwortdatei ohne BOM, wie von Windows Setup erwartet.</param>
    private static async Task AddAsync(ZipArchive zip, string name, string content, bool withBom, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var entryStream = await entry.OpenAsync(cancellationToken);
        var bytes = (withBom ? Encoding.UTF8.GetPreamble() : []).Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        await entryStream.WriteAsync(bytes, cancellationToken);
    }
}
