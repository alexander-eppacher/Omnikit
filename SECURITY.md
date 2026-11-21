# Sicherheitslücken melden / Reporting security issues

**Bitte keine öffentlichen Issues für Sicherheitslücken.** Melde sie privat über GitHub:
[Sicherheitslücke melden](https://github.com/alexander-eppacher/Omnikit/security/advisories/new) (Reiter „Security“ → „Report a vulnerability“).

*Please do not open public issues for security problems. Report them privately via GitHub's “Report a vulnerability” on the Security tab.*

## Was hilft

- Betroffene Version und Teil (App, Hilfsprozess, Agent, Server, Setup)
- Schritte zum Nachvollziehen und was ein Angreifer dadurch erreicht
- Wenn möglich ein Vorschlag zur Behebung

## Was passiert dann

- Eine erste Antwort kommt in der Regel innerhalb einer Woche (Einzelentwickler, kein Bereitschaftsdienst).
- Bestätigte Lücken werden mit einer Patch-Version behoben. Danach gibt es einen Sicherheitshinweis auf GitHub. Wer gemeldet hat, wird genannt, wenn gewünscht.
- Bis zum Fix bitte nichts veröffentlichen.

## Besonders interessant

- **Hilfsprozess (`Omnikit.Helper.exe`):** Kann ein Programm ohne Adminrechte ihn zu etwas bringen, das nicht bestätigt wurde, z. B. einen falschen Datenträger löschen?
- **Updates:** Lässt sich ein Update installieren, das nicht mit dem Projektschlüssel signiert ist?
- **Agent:** Kann ein Server oder jemand im Netz den Agent zu etwas anderem bringen als zu winget-Installationen bzw. zum signierten Selbst-Update?
- **Server:** Anmeldung, Rollen (Betrachter darf nichts ändern), Trennung der Geräte-Token.

## Nicht im Umfang

Angriffe, die schon Administrator- oder SYSTEM-Rechte auf dem PC voraussetzen. Angriffe mit dauerhaftem Zugriff auf die Hardware. Schwachstellen in winget-Paketen von Herstellern.
