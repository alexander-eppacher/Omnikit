# Verwendete Software anderer Projekte

Omnikit selbst steht unter der MIT-Lizenz (`LICENSE`). Die ausgelieferten Programme enthalten außerdem diese Bibliotheken (Stand: Version 4). Die vollständigen Lizenztexte stehen unter den angegebenen Adressen.

| Bibliothek | Wofür | Lizenz |
|---|---|---|
| .NET-Laufzeit und Microsoft.Extensions.* (Microsoft) | Grundlage von App, Hilfsprozess, Agent und Server | MIT – https://github.com/dotnet/runtime/blob/main/LICENSE.TXT |
| ASP.NET Core, Entity Framework Core, Microsoft.Data.Sqlite (Microsoft) | Server und Datenbank | MIT – https://github.com/dotnet/efcore/blob/main/LICENSE.txt |
| System.Management (Microsoft) | Windows-Daten über WMI | MIT – https://github.com/dotnet/runtime/blob/main/LICENSE.TXT |
| SQLitePCLRaw mit SQLite | Datenbankdatei des Servers | Apache-2.0 – https://github.com/ericsink/SQLitePCL.raw/blob/master/LICENSE.TXT; SQLite selbst: Public Domain – https://sqlite.org/copyright.html |
| Avalonia (Avalonia, Desktop, Themes.Fluent, Skia, HarfBuzz) | Oberfläche der App | MIT – https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md |
| Inter-Schrift (über Avalonia.Fonts.Inter) | Schriftart der App | SIL Open Font License 1.1 – https://github.com/rsms/inter/blob/master/LICENSE.txt |
| SkiaSharp, HarfBuzzSharp | Zeichnen und Textsatz | MIT – https://github.com/mono/SkiaSharp/blob/main/LICENSE.md |
| MicroCom.Runtime, Tmds.DBus.Protocol | von Avalonia genutzt | MIT |
| CommunityToolkit.Mvvm (.NET Foundation) | Aufbau der Oberfläche (MVVM) | MIT – https://github.com/CommunityToolkit/dotnet/blob/main/License.md |
| Serilog, Serilog.Extensions.Logging, Serilog.Sinks.File | Log-Dateien | Apache-2.0 – https://github.com/serilog/serilog/blob/dev/LICENSE |
| Inno Setup (Jordan Russell, Martijn Laan) | erzeugt die Setup-Programme | Inno Setup License – https://jrsoftware.org/files/is/license.txt |

Nur zum Entwickeln und Testen (nicht ausgeliefert): xUnit (Apache-2.0), Microsoft.AspNetCore.Mvc.Testing und Microsoft.EntityFrameworkCore.Design (MIT).

Omnikit verteilt keine Windows-Abbilder: Windows lädt die App bei Bedarf direkt von Microsoft. Programme installiert winget aus den Quellen der jeweiligen Hersteller.
