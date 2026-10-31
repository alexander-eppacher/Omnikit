# 0010 – Apps über winget, als normaler Benutzer

**Datum:** 31.10.2026

**Entscheidung:** Die Seite „Apps“ installiert und aktualisiert Programme ausschließlich über **winget** (Paketmanager von Windows) aus dessen offizieller Quelle. Omnikit lädt selbst nichts herunter und hat keinen eigenen Paketkatalog, nur eine Liste von Paket-IDs.

- **winget läuft als normaler Benutzer**, nicht über den Admin-Hilfsprozess (0005). Braucht ein Installer Adminrechte, fragt Windows selbst (UAC). Grund: winget lehnt mit Adminrechten Updates von Programmen ab, die nur für einen Benutzer installiert sind (Code `0x8A15007D`), und manche Installer verweigern Adminrechte (`0x8A150056`). Nachteil: Bei mehreren Programmen kann Windows mehrmals fragen.
- **Aufruf:** fester Pfad `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe` (kein Suchen über PATH), Argumente als Liste, Paket-IDs streng geprüft (nur Buchstaben, Ziffern und `. + - _`, kein führender Bindestrich). Immer `--exact`, `--silent`, `--accept-package-agreements`, `--accept-source-agreements`, `--disable-interactivity`; Installationen zusätzlich `--source winget`. Die Eingabe von winget wird sofort geschlossen, damit eine unerwartete Rückfrage nicht ewig wartet.
- **Lesen:** installierte Programme über `winget export` (JSON mit vollständigen IDs), Updates über die Tabelle von `winget upgrade`. Die Tabelle wird über die Trennlinie, die Spaltenzahl und die feste Spaltenreihenfolge gelesen, nicht über die (übersetzbaren) Überschriften; Zeilen werden von rechts gelesen, weil nur der Name Leerzeichen enthält. Gekürzte IDs („…“) werden über die Export-Liste aufgelöst oder weggelassen.
- **Fehler:** Rückgabecodes laut offizieller Liste (`returnCodes.md` im winget-cli-Repository) werden in verständliche Sätze übersetzt (kein Netz, Programm geöffnet, Neustart nötig, Richtlinie, Speicher voll …). Unbekannte Codes: „fehlgeschlagen“, Code in der Expertenansicht und im Log.
- **Ablauf:** Bestätigungsdialog mit allen Programmen, dann nacheinander (winget verträgt keine parallelen Installationen); „Nach diesem Programm aufhören“ statt hartem Abbruch, damit kein Installer mitten im Lauf beendet wird. Der App-Installer (winget selbst) wird immer zuletzt aktualisiert. Zeitlimit je Programm: 30 Minuten.
- **Vorlagen:** „Schul-PC“, „Büro“, „Gaming“ im Code; eigene Vorlagen als JSON-Datei (`SchemaVersion`, `Name`, `Packages`). Beim Laden werden ungültige IDs verworfen.
- Die Seite ist (wie „Leistung“) eine eigene Seite und kein `IModule`: Katalog mit Häkchen passt nicht in das Befund-Schema. Für den Agenten (Phase 5) kann später ein `IModule` dieselben Klassen nutzen.

**Verworfen:**
- *winget über den Admin-Hilfsprozess:* nur eine UAC-Abfrage, aber die oben genannten Fehler bei Benutzer-Installationen; außerdem bräuchte der Hilfsprozess dann Parameter und Fortschrittsmeldungen (größere Angriffsfläche).
- *COM-Schnittstelle / PowerShell-Modul `Microsoft.WinGet.Client`:* robuster beim Lesen, aber zusätzliche Abhängigkeiten bzw. ein Modul, das erst installiert werden müsste.
