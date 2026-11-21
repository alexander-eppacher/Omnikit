# Datenschutz

Omnikit sammelt so wenig wie möglich und schickt nichts an den Entwickler. Es gibt keine Telemetrie.

## Die App auf dem PC

Gespeichert wird nur auf diesem PC:

| Was | Wo | Inhalt |
|---|---|---|
| Einstellungen | `%APPDATA%\Omnikit\settings.json` | Ansicht, Update-Prüfung an/aus, Zeitpunkt der letzten Prüfung |
| Log-Dateien (7 Tage) | `%LOCALAPPDATA%\Omnikit\logs\` | Technische Abläufe, Windows-Version, Modell. **Keine** Seriennummer |
| Windows-Download, Installationsordner | `%LOCALAPPDATA%\Omnikit\downloads\`, `C:\Program Files\Omnikit Media\` | Dateien von Microsoft |
| Exportierte Berichte | wo der Nutzer sie speichert | Zustand des PCs, **ohne** Seriennummer |

Die App spricht nur mit diesen Diensten, und nur wenn es nötig ist:

- **GitHub** (Release-Seite des Projekts): bei jedem Start der App (und bei „Jetzt suchen“) die Frage nach einer neuen Version. Ohne Kennung des Geräts, abschaltbar („Nach Updates suchen“). GitHub sieht dabei wie bei jedem Webseitenaufruf die IP-Adresse.
- **Microsoft:** wenn ein Windows-Stick mit Download erstellt wird. Dabei werden Katalog und Windows-Dateien geladen.
- **winget-Quellen** (Microsoft bzw. die Hersteller): wenn Programme gesucht, installiert oder aktualisiert werden.

## Der Server (selbst gehostet)

Den Server betreibt die Schule, Firma oder der Verein selbst. **Verantwortlich für die Daten ist der Betreiber**, nicht das Omnikit-Projekt. Das Projekt sieht keine dieser Daten.

| Daten | Wozu | Wie lange |
|---|---|---|
| Pro Gerät: Computername, Gruppe, Zeitpunkt der Registrierung und der letzten Meldung, Windows-Version, Hersteller/Modell, ob virtueller PC, Version und Fähigkeiten des Agents | Geräteliste | bis das Gerät gelöscht wird |
| Pro Gerät: **nur der letzte** Bericht (Befunde zu Laufwerken, Speicherplatz, Akku, Treibern, BIOS) und die installierten Programme laut winget | Zustand und fehlende Programme anzeigen | wird bei jeder Meldung ersetzt; weg, wenn das Gerät gelöscht wird |
| Geräte-Token | Anmeldung des Agents | nur als SHA-256-Hash; weg mit dem Gerät |
| Aufgaben (Programm, wer angelegt und freigegeben hat, Ergebnis) | Nachvollziehen, was installiert wurde | bis das Gerät gelöscht wird |
| Benutzer der Verwaltung: Name, Rolle, Passwort | Anmeldung | Passwort nur als Hash; bis der Benutzer gelöscht wird |
| Audit-Log: wer wann was getan hat; bei **fehlgeschlagenen** Anmeldungen der eingegebene Name und die IP-Adresse | Nachvollziehbarkeit, Erkennen von Angriffen | wird derzeit nicht automatisch gelöscht |

Nicht gespeichert werden: Seriennummern, angemeldete Windows-Benutzer, Dateien oder Inhalte der PCs, Browserverläufe, Passwörter oder WLAN-Schlüssel.

**Löschen:** Ein Gerät löschen (Seite des Geräts → „Gerät löschen“) entfernt seine Daten, seinen Token und seine Aufgaben. Das Audit-Log behält die Einträge zu diesem Gerät. Den ganzen Server löschen: Volume `server-data` entfernen.

### Textbaustein für Betreiber

> Auf den PCs unserer Einrichtung läuft der Omnikit-Agent. Er meldet regelmäßig technische Zustandsdaten (Windows-Version, Modell, Zustand von Laufwerken und Akku, installierte Programme) an unseren eigenen Server unter <Adresse>. Personenbezogene Inhalte, angemeldete Benutzer oder Dateien werden nicht übertragen. Zweck ist die Wartung und Sicherheit der Geräte. Die Daten bleiben bei uns und werden gelöscht, wenn ein Gerät ausgemustert wird. Ansprechperson: <Name, Kontakt>.

**Hinweis für Firmen:** Wenn Mitarbeitende die PCs nutzen, kann schon eine Liste installierter Programme mitbestimmungspflichtig sein (z. B. nach dem Betriebsverfassungsgesetz in Deutschland). Das vor der Einführung klären.
