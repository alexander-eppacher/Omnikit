# Omnikit

Ein modulares Tool, das Windows-PCs, Laptops und virtuelle Maschinen **einrichtet, aktuell hält und auf ihren Zustand prüft** – mit einer einfachen Oberfläche, ohne IT-Abteilung.

> **Status:** Version 1 in Arbeit (Phase 2: Apps). Die App zeigt Systeminformationen, den Zustand von Laufwerken, Akku und Windows sowie die Live-Auslastung von CPU, Arbeitsspeicher, Laufwerken, Netzwerk und Grafik. Auf der Seite „Apps“ installiert und aktualisiert sie Programme über winget – nur nach Bestätigung. Sonst verändert sie nichts am System.

## Ansichten „Einfach“ und „Experte“

Unten in der Navigationsleiste lässt sich die Ansicht umschalten. **Einfach** zeigt Ampel, verständliche Sätze und das Wichtigste. **Experte** zeigt zusätzlich Fachwerte und Rohdaten, z. B. Firmware und Fehlerzähler der Laufwerke, Taktfrequenz, Caches, Prozesse/Threads/Handles, Verlauf je Prozessorkern oder IP-Adressen. Beide Ansichten zeigen dieselben Messwerte und dieselbe Ampel. Die Wahl wird gespeichert.

## Seite „Leistung“

Zeigt jede Sekunde die Auslastung wie der Task-Manager, mit Verlauf der letzten Minute. Gemessen wird nur, solange die Seite geöffnet ist. Bewertet wird der Durchschnitt über 30 Sekunden: Erst wenn Prozessor, Arbeitsspeicher oder ein Laufwerk eine halbe Minute lang zu mindestens 90 % ausgelastet sind, erscheint ein Hinweis. Kurze Spitzen lösen keinen Hinweis aus. Grafik und Netzwerk werden nur angezeigt, nicht bewertet. IP-Adressen werden nur angezeigt, nie gespeichert, geloggt oder exportiert.

## Seite „Apps“

Programme installieren und aktuell halten, über **winget**, den Paketmanager von Windows. Omnikit lädt selbst nichts herunter: Es gibt nur die Paket-ID an winget weiter, winget lädt aus seiner offiziellen Quelle.

- **Updates:** Liste aller Programme, für die winget eine neuere Version kennt. Auswählen und „Ausgewählte aktualisieren“.
- **Programme installieren:** Katalog mit rund 40 bekannten Programmen in Bereichen (Browser, Büro, Lernen, Spiele …). Installierte sind markiert. Vorlagen „Schul-PC“, „Büro“ und „Gaming“ setzen passende Häkchen; die eigene Auswahl lässt sich als JSON-Datei speichern und auf einem anderen PC wieder laden.
- Vor jeder Installation zeigt Omnikit, welche Programme installiert werden. Danach laufen sie nacheinander, mit Fortschritt und verständlicher Meldung je Programm (z. B. „das Programm ist gerade geöffnet“). Die Expertenansicht zeigt zusätzlich Paket-IDs, Quellen und Fehlercodes.
- winget läuft als normaler Benutzer. Braucht ein Programm Adminrechte, fragt Windows selbst nach – bei mehreren Programmen eventuell mehrmals.
- Fehlt winget (Teil des „App-Installers“ aus dem Microsoft Store), erklärt die Seite, was zu tun ist.

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Zielsystem: Windows 11 (Windows 10 wird nicht mehr unterstützt, siehe Entscheidung 0011). Unter Linux startet die App ebenfalls, zeigt aber Windows-spezifische Werte als „nicht verfügbar“.

## Bauen, testen, starten

```bash
dotnet build
dotnet test
dotnet run --project src/Omnikit.Desktop
```

## Windows-Version erzeugen

```bash
./scripts/publish-windows.sh
```

Ergebnis: `artifacts/win-x64/Omnikit.exe` – eine einzelne Datei, die ohne installiertes .NET läuft. Auf den Windows-PC kopieren und doppelklicken.

### Windows-Warnung beim ersten Start

Die App ist (noch) nicht mit einem Code-Signing-Zertifikat signiert. Windows SmartScreen zeigt deshalb „Der Computer wurde durch Windows geschützt“. Über **Weitere Informationen → Trotzdem ausführen** lässt sie sich starten. Manche Virenscanner melden Systemtools außerdem fälschlich, weil sie Hardwaredaten auslesen.

## Wo die App Dateien ablegt

| Was | Windows | Linux |
|---|---|---|
| Einstellungen | `%APPDATA%\Omnikit\settings.json` | `~/.config/Omnikit/settings.json` |
| Log-Dateien (7 Tage) | `%LOCALAPPDATA%\Omnikit\logs\` | `~/.local/share/Omnikit/logs/` |
| Kurzlebige Dateien (werden sofort gelöscht) | `%LOCALAPPDATA%\Omnikit\temp\` | `~/.local/share/Omnikit/temp/` |

Bis Oktober 2026 hieß die App „PcTool“. Eine vorhandene `settings.json` aus dem alten Ordner `PcTool` wird beim ersten Start einmalig übernommen.

Die Seriennummer des Geräts wird nur angezeigt, nicht gespeichert, nicht geloggt und nicht exportiert. Seriennummern von Laufwerken und Akku liest die App gar nicht erst aus.

## Adminrechte

Omnikit startet immer **ohne** Adminrechte. Für Installationen auf der Seite „Apps“ fragt Windows selbst nach, wenn ein Programm Adminrechte braucht. Nur für Laufwerksdetails (Verschleiß, Temperatur, Fehlerzähler) fragt die App auf Knopfdruck nach. Dann startet sie sich selbst unsichtbar ein zweites Mal mit Adminrechten (Windows zeigt dabei die UAC-Abfrage), liest nur diese Werte aus und beendet sich wieder. Dieser Hilfsprozess kann ausschließlich fest eingebaute Leseaktionen ausführen. Ohne Adminrechte funktioniert die App trotzdem vollständig; nur diese Werte fehlen dann. Der Hilfsprozess schreibt in eine eigene Log-Datei (`omnikit-helper-*.log`).

## Projektaufbau

| Ordner | Inhalt |
|---|---|
| `src/Omnikit.Core` | Modul-Schnittstelle, Systemerkennung, Einstellungen (keine UI) |
| `src/Omnikit.Modules.Health` | Modul „Gerätezustand“: Laufwerke, Speicherplatz, Akku, Windows |
| `src/Omnikit.Modules.Performance` | Seite „Leistung“: Live-Messung von CPU, RAM, Laufwerken, Netzwerk und Grafik |
| `src/Omnikit.Modules.Apps` | Seite „Apps“: winget-Anbindung, Katalog, Vorlagen |
| `src/Omnikit.Desktop` | Avalonia-Oberfläche |
| `tests/Omnikit.Core.Tests` | Unit-Tests für den Kern |
| `tests/Omnikit.Modules.Health.Tests` | Unit-Tests für das Health-Modul (u. a. Ampel-Grenzwerte) |
| `tests/Omnikit.Modules.Performance.Tests` | Unit-Tests für die Leistungsseite (Bewertung, Verlauf, Instanznamen) |
| `tests/Omnikit.Modules.Apps.Tests` | Unit-Tests für die Apps-Seite (winget-Ausgaben, Fehlercodes, Vorlagen) |
| `docs/decisions` | Wichtige Entscheidungen mit Begründung |

## Lizenz

MIT – siehe [LICENSE](LICENSE).

## Haftungsausschluss

Die Software wird ohne jede Gewährleistung bereitgestellt. Die Nutzung erfolgt auf eigene Verantwortung.
