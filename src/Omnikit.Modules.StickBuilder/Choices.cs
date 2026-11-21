using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Modules.StickBuilder;

/// <summary>Ein Eintrag in einer Auswahlliste: Wert für die Antwortdatei und Name für die Anzeige.</summary>
public sealed record Choice(string Value, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Auswahllisten für Sprache, Format, Tastatur, Zeitzone und vorinstallierte Apps.
/// Bewusst kurz gehalten: die Sprachen und Regionen der Zielgruppe (Deutschland, Österreich, Schweiz, Italien, Englisch).
/// Sprachen stehen wie bei Windows in ihrer eigenen Sprache und werden deshalb nicht übersetzt.
/// </summary>
public static class Choices
{
    public static IReadOnlyList<Choice> Languages { get; } =
    [
        new("de-DE", "Deutsch (Deutschland)"),
        new("en-US", "English (United States)"),
        new("en-GB", "English (United Kingdom)"),
        new("it-IT", "Italiano (Italia)"),
        new("fr-FR", "Français (France)"),
    ];

    public static IReadOnlyList<Choice> Formats { get; } =
    [
        new("de-DE", "Deutsch (Deutschland)"),
        new("de-AT", "Deutsch (Österreich)"),
        new("de-CH", "Deutsch (Schweiz)"),
        new("it-IT", "Italiano (Italia)"),
        new("en-US", "English (United States)"),
        new("en-GB", "English (United Kingdom)"),
        new("fr-FR", "Français (France)"),
    ];

    /// <summary>Format "Sprache:Tastaturlayout" (hexadezimal), wie Windows es erwartet.</summary>
    public static IReadOnlyList<Choice> Keyboards { get; } =
    [
        new("0407:00000407", "Deutsch"),
        new("0807:00000807", "Deutsch (Schweiz)"),
        new("0410:00000410", "Italiano"),
        new("0409:00000409", "English (US)"),
        new("0809:00000809", "English (UK)"),
        new("040c:0000040c", "Français"),
    ];

    public static IReadOnlyList<Choice> TimeZones { get; } =
    [
        new("W. Europe Standard Time", Texts.TimeZone_CentralEurope),
        new("GMT Standard Time", Texts.TimeZone_WesternEurope),
        new("Romance Standard Time", Texts.TimeZone_Romance),
        new("Eastern Standard Time", Texts.TimeZone_UsEast),
        new("Pacific Standard Time", Texts.TimeZone_UsWest),
        new("UTC", "UTC"),
    ];

    public static IReadOnlyList<Choice> Editions { get; } =
    [
        new(nameof(WindowsEdition.Home), "Windows 11 Home"),
        new(nameof(WindowsEdition.Pro), "Windows 11 Pro"),
        new(nameof(WindowsEdition.Education), "Windows 11 Education"),
        new(nameof(WindowsEdition.ProEducation), "Windows 11 Pro Education"),
    ];

    /// <summary>
    /// Vorinstallierte Apps, die man meist nicht braucht (Werbung, Spiele, Nachrichten).
    /// Wichtige Apps wie Store, Rechner, Fotos, Editor oder Terminal stehen bewusst nicht in der Liste.
    /// </summary>
    public static IReadOnlyList<Choice> RemovableApps { get; } =
    [
        new("Microsoft.BingNews", Texts.RemoveApp_News),
        new("Microsoft.BingWeather", Texts.RemoveApp_Weather),
        new("Microsoft.BingSearch", Texts.RemoveApp_BingSearch),
        new("Microsoft.GamingApp", Texts.RemoveApp_Xbox),
        new("Microsoft.MicrosoftSolitaireCollection", Texts.RemoveApp_Solitaire),
        new("Clipchamp.Clipchamp", Texts.RemoveApp_Clipchamp),
        new("Microsoft.Todos", Texts.RemoveApp_Todo),
        new("Microsoft.MicrosoftOfficeHub", Texts.RemoveApp_OfficeHub),
        new("Microsoft.OutlookForWindows", Texts.RemoveApp_Outlook),
        new("Microsoft.WindowsFeedbackHub", Texts.RemoveApp_FeedbackHub),
        new("Microsoft.GetHelp", Texts.RemoveApp_GetHelp),
        new("Microsoft.PowerAutomateDesktop", Texts.RemoveApp_PowerAutomate),
        new("MicrosoftCorporationII.MicrosoftFamily", Texts.RemoveApp_Family),
        new("Microsoft.Copilot", Texts.RemoveApp_Copilot),
    ];

    /// <summary>Was standardmäßig angehakt ist: Werbung, Spiele und Nachrichten.</summary>
    public static IReadOnlyList<string> DefaultRemovedApps { get; } =
    [
        "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.MicrosoftSolitaireCollection", "Clipchamp.Clipchamp",
        "Microsoft.WindowsFeedbackHub", "Microsoft.GetHelp",
    ];
}
