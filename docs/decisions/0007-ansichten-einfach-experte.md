# 0007 – Zwei Ansichten: „Einfach“ und „Experte“

**Datum:** 26.10.2026

**Entscheidung:** Ein Schalter unten in der Navigationsleiste wechselt für die ganze App zwischen „Einfach“ und „Experte“. Die Wahl wird sofort in den Einstellungen gespeichert (`AppSettings.ViewMode`, in der Datei als Text `"Simple"` bzw. `"Expert"`). Fehlt der Wert, z. B. in einer alten Datei, startet die App in der einfachen Ansicht.

- Die Expertenansicht zeigt **mehr, nicht andere** Werte: Ampel, Erklärungen und Messwerte sind in beiden Ansichten gleich.
- Ein Befund (`Finding`) kann zusätzlich **Rohwerte** (`Details`) haben, z. B. „Anschluss: NVMe · Typ: SSD · Firmware: …“. Sie erscheinen nur in der Expertenansicht.
- Befunde mit `IsExpertOnly` (z. B. Betriebsstunden) erscheinen nur in der Expertenansicht. Sie dürfen höchstens „Info“ sein, damit die Gesamtampel in der einfachen Ansicht nie ohne sichtbaren Grund gelb wird.
- Gefiltert wird nur die **Anzeige** (`ViewModeFilter`). Der Bericht und der JSON-Export bleiben immer vollständig.
- Akku: Die Kapazität in mWh stand bisher im Satz für Einsteiger. Jetzt steht sie bei den Rohwerten, der Satz nennt nur noch Prozent.
- Laufwerke: `TemperatureMax` bedeutet je nach Hersteller den höchsten gemessenen Wert oder die Warnschwelle. Deshalb wird es nur als Rohwert „laut Laufwerk“ gezeigt und nicht bewertet.

**Grund:** Zwei Zielgruppen (Einsteiger und Helpdesk/Profis) sollen dasselbe Werkzeug nutzen können. Einfachheit hat Vorrang (Projektziel); Profis brauchen trotzdem die genauen Werte.

**Verworfen:** Zwei getrennte Oberflächen (doppelter Pflegeaufwand); Rohwerte als ausklappbarer Bereich pro Befund (mehr Klicks für den Helpdesk, für Einsteiger trotzdem sichtbar).
