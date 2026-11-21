using System.Text;

namespace Omnikit.Modules.StickBuilder;

/// <summary>
/// So schreibt Windows Passwörter in Antwortdateien: Passwort und Elementname ("Password") hintereinander,
/// als UTF-16 kodiert und dann in Base64. Das ist nur eine <b>Verschleierung</b>, keine Verschlüsselung –
/// wer die Datei hat, kann das Passwort zurückrechnen. Darauf weist die Oberfläche hin.
/// </summary>
public static class UnattendPassword
{
    public static string Encode(string password, string elementName = "Password") =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(password + elementName));
}
