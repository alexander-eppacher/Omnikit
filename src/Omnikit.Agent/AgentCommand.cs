using System.Globalization;
using Omnikit.Agent.Resources;

namespace Omnikit.Agent;

/// <summary>Was beim Start gemacht werden soll (aus der Befehlszeile).</summary>
public abstract record AgentCommand
{
    /// <summary>Einmal am Server anmelden: Code gegen Token tauschen und speichern.</summary>
    public sealed record Register(Uri Server, string Code, string? CaFile) : AgentCommand;

    /// <summary>Nach „C:\Program Files\Omnikit Agent“ kopieren und als Dienst einrichten.</summary>
    public sealed record Install : AgentCommand;

    /// <summary>Dienst entfernen; Anmeldedaten löschen.</summary>
    public sealed record Uninstall : AgentCommand;

    /// <summary>Als Dienst laufen (so startet Windows den Agent).</summary>
    public sealed record Run : AgentCommand;

    /// <summary>Einmal melden und Aufgaben abholen, dann beenden (zum Ausprobieren).</summary>
    public sealed record Once : AgentCommand;

    /// <summary>Hilfe anzeigen; <see cref="Error"/> erklärt, was an der Eingabe falsch war.</summary>
    public sealed record Help(string? Error = null) : AgentCommand;

    public static AgentCommand Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new Help();
        }

        return args[0].ToLowerInvariant() switch
        {
            "register" => ParseRegister(args),
            "install" => new Install(),
            "uninstall" => new Uninstall(),
            "run" => new Run(),
            "once" => new Once(),
            _ => new Help(string.Format(CultureInfo.CurrentCulture, AgentTexts.Command_Unknown, args[0])),
        };
    }

    private static AgentCommand ParseRegister(IReadOnlyList<string> args)
    {
        string? server = null, code = null, ca = null;
        for (var i = 1; i < args.Count - 1; i += 2)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--server": server = args[i + 1]; break;
                case "--code": code = args[i + 1]; break;
                case "--ca": ca = args[i + 1]; break;
                default: return new Help(string.Format(CultureInfo.CurrentCulture, AgentTexts.Command_UnknownOption, args[i]));
            }
        }

        if (server is null || code is null)
        {
            return new Help(AgentTexts.Command_RegisterNeeds);
        }

        // Nur HTTPS (Regel 6.1.6): Token und Berichte gehen nie unverschlüsselt über das Netz
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new Help(AgentTexts.Command_HttpsOnly);
        }

        return new Register(uri, code, ca);
    }
}
