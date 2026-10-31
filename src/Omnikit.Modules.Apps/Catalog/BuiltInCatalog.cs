using Texts = Omnikit.Modules.Apps.Resources.AppsTexts;

namespace Omnikit.Modules.Apps.Catalog;

/// <summary>
/// Die Programme, die Omnikit zum Installieren anbietet. Es gibt keinen eigenen Download: Omnikit gibt nur die
/// Paket-ID an winget weiter, winget lädt aus seiner offiziellen Quelle.
/// Alle IDs wurden am 31.10.2026 mit „winget show --id … --exact“ geprüft.
/// </summary>
public static class BuiltInCatalog
{
    public static IReadOnlyList<CatalogApp> Apps { get; } =
    [
        new("Mozilla.Firefox", "Firefox", AppCategory.Browser, Texts.App_Firefox),
        new("Google.Chrome", "Google Chrome", AppCategory.Browser, Texts.App_Chrome),
        new("Brave.Brave", "Brave", AppCategory.Browser, Texts.App_Brave),

        new("Zoom.Zoom", "Zoom", AppCategory.Communication, Texts.App_Zoom),
        new("Microsoft.Teams", "Microsoft Teams", AppCategory.Communication, Texts.App_Teams),
        new("Discord.Discord", "Discord", AppCategory.Communication, Texts.App_Discord),
        new("Mozilla.Thunderbird", "Thunderbird", AppCategory.Communication, Texts.App_Thunderbird),
        new("OpenWhisperSystems.Signal", "Signal", AppCategory.Communication, Texts.App_Signal),

        new("TheDocumentFoundation.LibreOffice", "LibreOffice", AppCategory.Office, Texts.App_LibreOffice),
        new("Adobe.Acrobat.Reader.64-bit", "Adobe Acrobat Reader", AppCategory.Office, Texts.App_AcrobatReader),
        new("SumatraPDF.SumatraPDF", "SumatraPDF", AppCategory.Office, Texts.App_SumatraPdf),
        new("Notepad++.Notepad++", "Notepad++", AppCategory.Office, Texts.App_NotepadPlusPlus),
        new("KeePassXCTeam.KeePassXC", "KeePassXC", AppCategory.Office, Texts.App_KeePassXc),
        new("Bitwarden.Bitwarden", "Bitwarden", AppCategory.Office, Texts.App_Bitwarden),

        new("VideoLAN.VLC", "VLC media player", AppCategory.Media, Texts.App_Vlc),
        new("Spotify.Spotify", "Spotify", AppCategory.Media, Texts.App_Spotify),
        new("Audacity.Audacity", "Audacity", AppCategory.Media, Texts.App_Audacity),
        new("OBSProject.OBSStudio", "OBS Studio", AppCategory.Media, Texts.App_Obs),
        new("GIMP.GIMP.3", "GIMP", AppCategory.Media, Texts.App_Gimp),
        new("Inkscape.Inkscape", "Inkscape", AppCategory.Media, Texts.App_Inkscape),
        new("HandBrake.HandBrake", "HandBrake", AppCategory.Media, Texts.App_HandBrake),
        new("ShareX.ShareX", "ShareX", AppCategory.Media, Texts.App_ShareX),
        new("Greenshot.Greenshot", "Greenshot", AppCategory.Media, Texts.App_Greenshot),

        new("7zip.7zip", "7-Zip", AppCategory.Tools, Texts.App_7Zip),
        new("voidtools.Everything", "Everything", AppCategory.Tools, Texts.App_Everything),
        new("Microsoft.PowerToys", "PowerToys", AppCategory.Tools, Texts.App_PowerToys),
        new("WinDirStat.WinDirStat", "WinDirStat", AppCategory.Tools, Texts.App_WinDirStat),
        new("CrystalDewWorld.CrystalDiskInfo", "CrystalDiskInfo", AppCategory.Tools, Texts.App_CrystalDiskInfo),

        new("GeoGebra.Classic", "GeoGebra Classic", AppCategory.Learning, Texts.App_GeoGebra),
        new("MITMediaLab.Scratch.3", "Scratch 3", AppCategory.Learning, Texts.App_Scratch),
        new("Anki.Anki", "Anki", AppCategory.Learning, Texts.App_Anki),

        new("Valve.Steam", "Steam", AppCategory.Games, Texts.App_Steam),
        new("EpicGames.EpicGamesLauncher", "Epic Games Launcher", AppCategory.Games, Texts.App_Epic),
        new("GOG.Galaxy", "GOG Galaxy", AppCategory.Games, Texts.App_Gog),
        new("ElectronicArts.EADesktop", "EA app", AppCategory.Games, Texts.App_Ea),
        new("Ubisoft.Connect", "Ubisoft Connect", AppCategory.Games, Texts.App_Ubisoft),

        new("Microsoft.VisualStudioCode", "Visual Studio Code", AppCategory.Development, Texts.App_VsCode),
        new("Git.Git", "Git", AppCategory.Development, Texts.App_Git),
        new("Python.Python.3.13", "Python 3.13", AppCategory.Development, Texts.App_Python),
        new("OpenJS.NodeJS.LTS", "Node.js (LTS)", AppCategory.Development, Texts.App_NodeJs),
        new("Microsoft.WindowsTerminal", "Windows Terminal", AppCategory.Development, Texts.App_WindowsTerminal),
    ];

    /// <summary>Fertige Auswahl für typische Einsätze. Enthält nur Programme aus dem Katalog.</summary>
    public static IReadOnlyList<AppTemplate> Templates { get; } =
    [
        new(Texts.Template_School,
        [
            "Mozilla.Firefox", "TheDocumentFoundation.LibreOffice", "VideoLAN.VLC", "7zip.7zip", "SumatraPDF.SumatraPDF",
            "GeoGebra.Classic", "MITMediaLab.Scratch.3", "Audacity.Audacity",
        ]),
        new(Texts.Template_Office,
        [
            "Mozilla.Firefox", "TheDocumentFoundation.LibreOffice", "Adobe.Acrobat.Reader.64-bit", "7zip.7zip", "Zoom.Zoom",
            "Microsoft.Teams", "Mozilla.Thunderbird", "KeePassXCTeam.KeePassXC", "Notepad++.Notepad++",
        ]),
        new(Texts.Template_Gaming,
        [
            "Valve.Steam", "EpicGames.EpicGamesLauncher", "Discord.Discord", "OBSProject.OBSStudio", "VideoLAN.VLC", "7zip.7zip",
        ]),
    ];

    public static string CategoryName(AppCategory category) => category switch
    {
        AppCategory.Browser => Texts.Category_Browser,
        AppCategory.Communication => Texts.Category_Communication,
        AppCategory.Office => Texts.Category_Office,
        AppCategory.Media => Texts.Category_Media,
        AppCategory.Tools => Texts.Category_Tools,
        AppCategory.Learning => Texts.Category_Learning,
        AppCategory.Games => Texts.Category_Games,
        AppCategory.Development => Texts.Category_Development,
        _ => category.ToString(),
    };
}
