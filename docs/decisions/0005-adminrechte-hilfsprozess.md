# 0005 – Adminrechte über einen unsichtbaren Hilfsprozess

**Datum:** 24.10.2026

**Entscheidung:** Werte, die Adminrechte brauchen (zunächst nur Laufwerksdetails), werden über einen Hilfsprozess gelesen. Das ist seit 11.11.2026 eine eigene kleine `Omnikit.Helper.exe` ohne Oberflächen-Bibliotheken (vorher dieselbe `Omnikit.exe`), gestartet über ihren absoluten Pfad im Ordner der App mit `--elevated-operation <Aktion> <Pipe>`; ihr Manifest verlangt Adminrechte. Sie öffnet kein Fenster, führt genau eine Aktion aus einer fest registrierten Liste (`IElevatedOperation`) aus, schickt das Ergebnis über eine Named Pipe mit zufälligem Namen zurück und beendet sich.

- Die App startet immer ohne Adminrechte; der Hilfsprozess wird nur auf Knopfdruck gestartet.
- **Eigene Datei (11.11.2026, 0017):** Mit Adminrechten läuft nur Code, der dafür nötig ist – keine Oberfläche, keine Benutzereinstellungen. App und Hilfsprozess liegen als Ordner nebeneinander (kein Single-File mehr), damit beim Start nichts in einen Benutzerordner wie `%TEMP%` entpackt und von dort mit Adminrechten geladen wird. Die erlaubten Aktionen stehen an einer Stelle (`Omnikit.Operations/ElevatedOperations.cs`) und gelten für App und Hilfsprozess.
- Vorher erklärt ein Hinweisfenster, was passiert. Der Text hängt davon ab, ob das Windows-Konto ein Admin-Konto ist (dann nur „Ja“ klicken) oder ein Standardkonto (dann Benutzername und Passwort eines Administrators nötig).
- Lehnt der Nutzer ab oder fehlen die Rechte, funktioniert die App weiter; nur diese Werte fehlen.
- Die UAC-Abfrage von Windows wird nicht umgangen (das wäre eine Technik von Schadsoftware).
- **Pipe (11.11.2026):** Verbinden dürfen sich nur der angemeldete Benutzer und Administratoren (bei einem Standardkonto läuft der Hilfsprozess unter dem Admin-Konto, das in der UAC-Abfrage eingegeben wurde). Gibt es den zufälligen Namen schon, schlägt das Anlegen fehl. Der Hilfsprozess verbindet sich nur mit der Stufe „Identification“: Die App erfährt, wer sich verbindet, kann aber nicht mit dessen Adminrechten handeln. Der Hilfsprozess liest nie etwas aus der Pipe – er schreibt nur Fortschritt und Ergebnis.
- Jeder Fehler einer Aktion wird zu „fehlgeschlagen“ (Details ins Log) – im Hilfsprozess ebenso wie wenn die App schon als Admin läuft und die Aktion ohne Hilfsprozess ausführt.

**Grund:** Regel 6.1.5 (nicht die ganze App als Admin), Wunsch des Entwicklers nach „unsichtbarem“ Ablauf und nach Nutzbarkeit ohne Adminrechte. Eine einzige .exe bleibt verteilbar.

**Verworfen:** App komplett als Admin neu starten (verstößt gegen 6.1.5); Windows-Dienst (dauerhafter Eingriff; als Idee für den Agenten in Phase 5 vorgemerkt).
