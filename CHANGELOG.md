# Änderungen

Neueste Version oben. Versionsnummern: Haupt.Neben.Patch, eine Nummer für App, Agent und Server.

## 4.0.0 – 21.11.2026

### Neu
- **Installer** für App und Agent. Die App wird nach `C:\Program Files\Omnikit` installiert, der Agent nach `C:\Program Files\Omnikit Agent`; das Agent-Setup lässt sich still mit Server-Adresse und Code einrichten.
- **Updates:** Die App fragt höchstens einmal am Tag nach einer neuen Version (abschaltbar, ohne Kennung) und installiert sie per Knopf. Der Agent lässt sich vom Server aus aktualisieren. Beide installieren nur Updates mit der Signatur des Projekts.
- **„Als Text kopieren“:** den Bericht einer Seite als lesbaren Text in die Zwischenablage, z. B. für ein Helpdesk-Ticket.
- Portable Version als ZIP (49 MB statt rund 100 MB).

### Sicherheit
- Der **Hilfsprozess** mit Adminrechten ist jetzt eine eigene kleine Datei (`Omnikit.Helper.exe`) ohne Oberfläche. Vor dem Löschen eines USB-Sticks fragt er selbst noch einmal nach, mit dem Datenträger, den er selbst geprüft hat.
- Keine Single-File-Programme mehr, die beim Start Bibliotheken in einen Temp-Ordner entpacken.

### Server
- Nur noch **SQLite** (eine Datei im Volume `server-data`); PostgreSQL entfällt.
- **Datenbank-Migrationen** mit automatischer Sicherung vor jeder Änderung; der Server startet nicht mit einer neueren oder alten Vorab-Datenbank.
- Audit-Log speichert Ereignisse und Werte statt fertiger Sätze (später übersetzbar).
- Agent-Schnittstelle mit Version (`/api/agent/v1/…`); der Agent meldet Version und Fähigkeiten, der Server bietet nur an, was er kann.

### Für Betreiber wichtig
- **Datenbanken aus Vorabversionen werden nicht übernommen** (vorher `EnsureCreated`, PostgreSQL). Nach dem Update startet der Server mit einer klaren Meldung nicht; Datei löschen bzw. neu aufsetzen.
- Agents vor 4.0 sprechen die alte Schnittstelle und müssen neu installiert werden.

## 1.0.0 – 31.10.2026

Erste Version: Übersicht, Gerätezustand, Leistung, Apps (winget), Ansichten „Einfach“ und „Experte“.
