# Omnikit

Ein modulares Tool, das Windows-PCs, Laptops und virtuelle Maschinen **einrichtet, aktuell hält und auf ihren Zustand prüft** – mit einer einfachen Oberfläche, ohne IT-Abteilung.

> **Status:** Version 1 veröffentlicht (`v1.0.0`). Version 4 ist in Arbeit: Installer, signierte Updates, Hilfsprozess als eigene Datei. Gebaut sind auch Windows-Stick, Treiber, Server und Agent sowie BIOS; sie warten noch auf ihre Abnahmetests. Die App liest nur. Ändern tut sie nur nach Bestätigung (Programme installieren, USB-Stick erstellen), der Agent nur nach Freigabe im Server.

*English:* Omnikit sets up, updates and checks Windows 11 PCs, laptops and VMs with a simple interface – for schools, small businesses and clubs without IT staff. It reads by default and changes things only after confirmation; an optional self-hosted server manages many PCs. The interface is German for now (English is planned). Security issues: see [SECURITY.md](SECURITY.md).

## Installieren

- **Setup (empfohlen):** `Omnikit-<Version>-setup.exe` von der [Release-Seite](https://github.com/alexander-eppacher/Omnikit/releases) starten. Omnikit wird nach `C:\Program Files\Omnikit` installiert. Nur dort können ausschließlich Administratoren die Dateien austauschen, und nur dort aktualisiert sich Omnikit selbst.
- **Portabel:** `Omnikit-<Version>-portabel.zip` entpacken und `Omnikit.exe` starten. Kann alles, was die installierte Version kann, aktualisiert sich aber nicht selbst. Die App zeigt dann nur einen Hinweis mit Link zur Release-Seite.
- **Prüfsummen:** Zu jedem Release gehört eine Datei `SHA256SUMS`. In PowerShell vergleichen: `Get-FileHash .\Omnikit-<Version>-setup.exe`.

**Updates:** Höchstens einmal am Tag fragt Omnikit auf der Release-Seite nach einer neuen Version, ohne Kennung des Geräts. Das lässt sich unten links mit „Nach Updates suchen“ abschalten. Installiert wird ein Update nur, wenn es mit dem Schlüssel des Projekts signiert ist. Die Prüfung macht der Hilfsprozess mit Adminrechten, an einem Ort, den kein anderes Programm verändern kann (Ablauf: [docs/Release.md](docs/Release.md)).

## Ansichten „Einfach“ und „Experte“

Unten in der Navigationsleiste lässt sich die Ansicht umschalten. **Einfach** zeigt Ampel, verständliche Sätze und das Wichtigste. **Experte** zeigt zusätzlich Fachwerte und Rohdaten, z. B. Firmware und Fehlerzähler der Laufwerke, Taktfrequenz, Caches, Prozesse/Threads/Handles, Verlauf je Prozessorkern oder IP-Adressen. Beide Ansichten zeigen dieselben Messwerte und dieselbe Ampel. Die Wahl wird gespeichert.

## Seite „Treiber“

Zeigt Geräte, die Windows mit einem Fehler meldet (im Geräte-Manager mit gelbem Ausrufezeichen), mit einer verständlichen Abhilfe – z. B. „kein Treiber installiert“. Erkennt auch, wenn die Grafik nur mit dem Notfalltreiber von Windows läuft. Ausgeschaltete Geräte erscheinen nur als Hinweis. Die Expertenansicht listet die Treiber von Grafik, Netzwerk, Audio, Bluetooth und Kamera mit Hersteller, Version und Datum. Omnikit installiert keine Treiber; die Knöpfe „Optionale Updates öffnen“ und „Geräte-Manager öffnen“ führen zu den passenden Stellen in Windows.

## Seite „BIOS“

Zeigt BIOS-Version und -Datum, ob der PC im UEFI-Modus startet und ob Secure Boot an ist. Für bekannte Hersteller gibt es einen Knopf zur offiziellen Support-Seite, wo die BIOS-Updates liegen. **Omnikit flasht nie ein BIOS** und vergleicht keine Versionen (dafür gibt es keine zuverlässige offizielle Quelle).

## Seite „Leistung“

Zeigt jede Sekunde die Auslastung wie der Task-Manager, mit Verlauf der letzten Minute. Gemessen wird nur, solange die Seite geöffnet ist. Bewertet wird der Durchschnitt über 30 Sekunden: Erst wenn Prozessor, Arbeitsspeicher oder ein Laufwerk eine halbe Minute lang zu mindestens 90 % ausgelastet sind, erscheint ein Hinweis. Kurze Spitzen lösen keinen Hinweis aus. Grafik und Netzwerk werden nur angezeigt, nicht bewertet. IP-Adressen werden nur angezeigt, nie gespeichert, geloggt oder exportiert.

## Seite „Apps“

Programme installieren und aktuell halten, über **winget**, den Paketmanager von Windows. Omnikit lädt selbst nichts herunter: Es gibt nur die Paket-ID an winget weiter, winget lädt aus seiner offiziellen Quelle.

- **Updates:** Liste aller Programme, für die winget eine neuere Version kennt. Auswählen und „Ausgewählte aktualisieren“.
- **Programme installieren:** Katalog mit rund 40 bekannten Programmen in Bereichen (Browser, Büro, Lernen, Spiele …). Installierte sind markiert. Vorlagen „Schul-PC“, „Büro“ und „Gaming“ setzen passende Häkchen; die eigene Auswahl lässt sich als JSON-Datei speichern und auf einem anderen PC wieder laden.
- Vor jeder Installation zeigt Omnikit, welche Programme installiert werden. Danach laufen sie nacheinander, mit Fortschritt und verständlicher Meldung je Programm (z. B. „das Programm ist gerade geöffnet“). Die Expertenansicht zeigt zusätzlich Paket-IDs, Quellen und Fehlercodes.
- winget läuft als normaler Benutzer. Braucht ein Programm Adminrechte, fragt Windows selbst nach – bei mehreren Programmen eventuell mehrmals.
- Fehlt winget (Teil des „App-Installers“ aus dem Microsoft Store), erklärt die Seite, was zu tun ist.

## Seite „Windows-Stick“

Erstellt startfähige USB-Sticks – wie Rufus – und richtet auf Wunsch eine automatische Windows-Installation ein.

- **Quelle:** Windows 11 direkt von Microsoft laden (Sprache wählen; derselbe offizielle Katalog wie beim Media Creation Tool, jede Datei wird anhand von Microsofts Prüfsumme kontrolliert) oder eine eigene ISO-Datei (Windows oder z. B. Linux).
- **Stick:** Angeboten werden nur USB-Sticks, die Windows als Wechselmedium meldet. USB-Festplatten und -SSDs (und manche schnelle Sticks) erscheinen nur mit dem Häkchen „Auch USB-Festplatten und -SSDs anzeigen“ – mit deutlicher Warnung, weil darauf oft Sicherungen liegen. Vor dem Löschen kommt eine Warnung, dann fragt Windows nach Adminrechten. System- und Startlaufwerke, schreibgeschützte Sticks und Laufwerke über 256 GB lehnt Omnikit ab. Direkt vor dem Löschen prüft Omnikit noch einmal, ob es genau der bestätigte Stick ist (z. B. falls er inzwischen umgesteckt wurde). Liegt die ISO-Datei oder Omnikit selbst auf diesem Stick, wird abgebrochen.
- **Automatische Installation (wie der Unattend-Generator von Schneegans):** Sprache, Tastatur, Zeitzone, Edition, lokales Konto statt Microsoft-Konto, optional Festplatte automatisch einteilen, Programme aus dem Katalog (werden nach der ersten Anmeldung über winget installiert), vorinstallierte Werbe-Apps entfernen, Datenschutz-Grundeinstellungen, Explorer und Taskleiste (Dateiendungen, Startknopf links, Widgets, klassisches Kontextmenü, keine Websuche im Start), System (Passwort läuft nicht ab, Schnellstart aus, keine automatische Geräteverschlüsselung, Installation auf älteren PCs ohne TPM 2.0 oder Secure Boot), WLAN vorab eintragen (das Passwort wird danach aus der Kopie der Antwortdatei auf dem PC entfernt). Ergebnis ist eine `autounattend.xml`, die Omnikit an die richtige Stelle auf dem Stick legt. Eine vorhandene `autounattend.xml` (z. B. von schneegans.de) lässt sich importieren. Alternativ alles als ZIP mit Anleitung.
- Das Passwort des Kontos steht in der Antwortdatei nur verschleiert, nicht verschlüsselt. Windows wird mit Microsofts öffentlichen „generischen“ Schlüsseln installiert, die nur die Edition wählen und nichts aktivieren.
- Vorbild für den Aufbau der Antwortdatei: [Unattend-Generator von Christoph Schneegans](https://schneegans.de/windows/unattend-generator/) (MIT-Lizenz); es wurde kein Code übernommen.

## Server und Agent (mehrere PCs zentral verwalten)

Optional und selbst gehostet. Der **Server** zeigt alle PCs mit Ampel, den letzten Bericht (Gerätezustand, Treiber, BIOS) und die installierten Programme. Gruppen (z. B. „Computerraum 1“) haben Pflichtprogramme; fehlende werden als Aufgaben angelegt. **Jede Aufgabe läuft erst nach der Freigabe durch einen Admin.** Rollen: Admin und Betrachter. Jede Änderung steht im Audit-Log. Der **Agent** ist ein Windows-Dienst auf jedem PC: Er meldet alle 30 Minuten den Zustand und fragt jede Minute nach freigegebenen Aufgaben (am PC ist kein Port offen). Er kann nur Programme über winget installieren oder aktualisieren – keine beliebigen Befehle.

**Server starten** (Docker mit Compose):
```bash
cd deploy
cp .env.example .env      # ausfüllen: Name des Servers und Admin-Passwort
docker compose up -d      # Server (Datenbank als Datei im Volume) und Caddy (HTTPS)
```
Danach im Browser `https://<OMNIKIT_DOMAIN>/` öffnen und als `admin` anmelden.
**Daten und Sicherung:** Die Datenbank ist eine einzige Datei (`omnikit.db` im Volume `server-data`). Vor jeder Aktualisierung ihres Aufbaus legt der Server daneben eine Sicherung an (`omnikit.db.vor-<Datum>`). Ist die Datenbank neuer als der Server (z. B. nach einer Rückkehr zu einer älteren Version), startet er nicht und nennt den Grund im Log (`docker compose logs server`).

Der Server nimmt nur HTTPS an. Wer ihn ohne das mitgelieferte Compose hinter einem eigenen Reverse Proxy betreibt, setzt `Server__TrustProxy=true` – und nur dann, wenn der Server selbst von außen nicht erreichbar ist.

**Agent auf einem PC einrichten** – mit dem Setup (auch still, z. B. für viele PCs):
```bat
OmnikitAgent-<Version>-setup.exe /VERYSILENT /SERVER=https://<OMNIKIT_DOMAIN>/ /CODE=<Code aus „Codes“>
```
Oder von Hand aus der ZIP-Datei (Eingabeaufforderung mit Adminrechten):
```bat
Omnikit.Agent.exe register --server https://<OMNIKIT_DOMAIN>/ --code <Code aus „Codes“>
Omnikit.Agent.exe install
```
Ein installierter Agent lässt sich im Server über „Agent aktualisieren“ auf die neueste Version bringen. Der Server gibt dabei nur den Auftrag. Woher das Update kommt und ob es echt ist, prüft der Agent selbst.
Mit `OMNIKIT_TLS=internal` (LAN ohne öffentliche Domain) braucht der Agent Caddys Stammzertifikat:
`docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt server-ca.crt` und beim Registrieren `--ca server-ca.crt` anhängen (beim Setup: `/CA=C:\pfad\server-ca.crt`). Der Agent vertraut dann nur noch diesem Zertifikat. Entfernen: in Windows unter „Apps“ deinstallieren bzw. `Omnikit.Agent.exe uninstall`. Den Agent baut `./scripts/publish-agent.sh`.

Welche Daten der Server speichert und wie lange: [docs/Datenschutz.md](docs/Datenschutz.md).

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

Ergebnis: der Ordner `artifacts/win-x64/Omnikit/` mit `Omnikit.exe` (die App) und `Omnikit.Helper.exe` (der Hilfsprozess für Adminrechte), dazu dieselbe Version als ZIP (`Omnikit-<Version>-portabel.zip`). Läuft ohne installiertes .NET: ZIP auf dem Windows-PC entpacken und `Omnikit.exe` starten. Beide Dateien müssen im selben Ordner bleiben.

Die Setups baut `scripts/build-installers.ps1` mit [Inno Setup 6](https://jrsoftware.org/isinfo.php) unter Windows. Für die CI gibt es dasselbe als `scripts/publish-all.ps1`. Releases entstehen aus einem Tag über GitHub Actions als Entwurf und werden erst nach Prüfung und Signatur veröffentlicht ([docs/Release.md](docs/Release.md)).

### Windows-Warnung beim ersten Start

Die App ist (noch) nicht mit einem Code-Signing-Zertifikat signiert. Ein kostenloses Zertifikat für Open-Source-Projekte ist beantragt. Windows SmartScreen zeigt deshalb „Der Computer wurde durch Windows geschützt“. Über **Weitere Informationen → Trotzdem ausführen** lässt sie sich starten. Wer sichergehen will, vergleicht vorher die Prüfsumme mit `SHA256SUMS` aus dem Release. Manche Virenscanner melden Systemtools außerdem fälschlich, weil sie Hardwaredaten auslesen.

## Wo die App Dateien ablegt

| Was | Windows | Linux |
|---|---|---|
| Einstellungen | `%APPDATA%\Omnikit\settings.json` | `~/.config/Omnikit/settings.json` |
| Log-Dateien (7 Tage) | `%LOCALAPPDATA%\Omnikit\logs\` | `~/.local/share/Omnikit/logs/` |
| Kurzlebige Dateien (werden sofort gelöscht) | `%LOCALAPPDATA%\Omnikit\temp\` | `~/.local/share/Omnikit/temp/` |
| Von Microsoft geladenes Windows (ca. 5 GB, für weitere Sticks) | `%LOCALAPPDATA%\Omnikit\downloads\` | – |
| Daraus gebauter Installationsordner (ca. 6 GB, nur für Administratoren) | `C:\Program Files\Omnikit Media\` | – |

Bis Oktober 2026 hieß die App „PcTool“. Eine vorhandene `settings.json` aus dem alten Ordner `PcTool` wird beim ersten Start einmalig übernommen.

Die Seriennummer des Geräts wird nur angezeigt, nicht gespeichert, nicht geloggt und nicht exportiert. Seriennummern von Laufwerken und Akku liest die App gar nicht erst aus.

## Adminrechte

Omnikit startet immer **ohne** Adminrechte. Für Installationen auf der Seite „Apps“ fragt Windows selbst nach, wenn ein Programm Adminrechte braucht. Für Laufwerksdetails (Verschleiß, Temperatur, Fehlerzähler) und zum Erstellen eines USB-Sticks fragt die App auf Knopfdruck nach. Dann startet sie den kleinen Hilfsprozess `Omnikit.Helper.exe` aus ihrem eigenen Ordner mit Adminrechten (Windows zeigt dabei die UAC-Abfrage). Er hat keine Oberfläche, führt genau diese eine Aktion aus und beendet sich wieder. Dieser Hilfsprozess kann ausschließlich fest eingebaute Aktionen ausführen und prüft deren Angaben selbst (z. B. dass ein Datenträger wirklich ein USB-Stick ist). Ohne Adminrechte funktioniert die App trotzdem vollständig; nur diese Werte fehlen dann. Der Hilfsprozess schreibt in eine eigene Log-Datei (`omnikit-helper-*.log`).

## Projektaufbau

| Ordner | Inhalt |
|---|---|
| `src/Omnikit.Core` | Modul-Schnittstelle, Systemerkennung, Einstellungen (keine UI) |
| `src/Omnikit.Modules.Health` | Modul „Gerätezustand“: Laufwerke, Speicherplatz, Akku, Windows |
| `src/Omnikit.Modules.Drivers` | Modul „Treiber“: Geräte mit Fehlern, Notfall-Grafiktreiber, Treiberliste |
| `src/Omnikit.Modules.Bios` | Modul „BIOS“: Version, Startmodus, Secure Boot, Support-Seite |
| `src/Omnikit.Modules.Performance` | Seite „Leistung“: Live-Messung von CPU, RAM, Laufwerken, Netzwerk und Grafik |
| `src/Omnikit.Modules.Apps` | Seite „Apps“: winget-Anbindung, Katalog, Vorlagen |
| `src/Omnikit.Modules.StickBuilder` | Seite „Windows-Stick“: Antwortdatei, Skripte, Stick erstellen, Windows-Download |
| `src/Omnikit.Desktop` | Avalonia-Oberfläche |
| `src/Omnikit.Shared` | Verträge zwischen Agent und Server |
| `src/Omnikit.Server` | Server: Agent-Schnittstelle und Verwaltungsseiten (Razor Pages) |
| `src/Omnikit.Agent` | Agent: Windows-Dienst |
| `deploy` | Dockerfile, docker-compose.yml, Caddyfile, Vorlage für .env |
| `tests/Omnikit.Core.Tests` | Unit-Tests für den Kern |
| `tests/Omnikit.Modules.Health.Tests` | Unit-Tests für das Health-Modul (u. a. Ampel-Grenzwerte) |
| `tests/Omnikit.Modules.Drivers.Tests` | Unit-Tests für das Treiber-Modul (Fehlercodes, Filter) |
| `tests/Omnikit.Modules.Bios.Tests` | Unit-Tests für das BIOS-Modul (Bewertung, Hersteller-Links) |
| `tests/Omnikit.Server.Tests` | Tests für den Server (Registrierung, Aufgaben, Rechte, Schnittstelle) |
| `tests/Omnikit.Agent.Tests` | Tests für den Agent (Befehle, Aufgabenprüfung, Zertifikate) |
| `tests/Omnikit.Modules.Performance.Tests` | Unit-Tests für die Leistungsseite (Bewertung, Verlauf, Instanznamen) |
| `tests/Omnikit.Modules.Apps.Tests` | Unit-Tests für die Apps-Seite (winget-Ausgaben, Fehlercodes, Vorlagen) |
| `tests/Omnikit.Modules.StickBuilder.Tests` | Unit-Tests für den Stick-Builder (Antwortdatei, Prüfungen, Katalog) |
| `docs/decisions` | Wichtige Entscheidungen mit Begründung |

## Lizenz

MIT – siehe [LICENSE](LICENSE). Verwendete Bibliotheken und ihre Lizenzen: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Sicherheit

Sicherheitslücken bitte nicht als öffentliches Issue melden, sondern privat, siehe [SECURITY.md](SECURITY.md).

## Haftungsausschluss

Die Software wird ohne jede Gewährleistung bereitgestellt (siehe MIT-Lizenz). Die Nutzung erfolgt auf eigene Verantwortung. Besonders wichtig:

- **USB-Stick erstellen löscht den gewählten Datenträger vollständig.** Omnikit lässt nur Wechseldatenträger zu (USB-Festplatten nur nach ausdrücklichem Häkchen) und fragt zweimal nach. Vor dem Bestätigen trotzdem prüfen, ob es der richtige Stick ist.
- **Die automatische Installation vom Stick kann die interne Platte eines PCs löschen**, wenn diese Option gewählt wurde. Einen solchen Stick nie an einem PC mit wichtigen Daten starten.
- **Programme installieren und aktualisieren** geschieht über winget mit den Paketen der jeweiligen Hersteller. Omnikit hat auf deren Inhalt keinen Einfluss.
- **Server-Betreiber** (Schule, Firma, Verein) sind für die Daten ihrer Geräte verantwortlich, siehe [docs/Datenschutz.md](docs/Datenschutz.md).
