# 0008 – Seite „Leistung“: Datenquellen und Bewertung

**Datum:** 30.10.2026

**Entscheidung:** Neues Projekt `Omnikit.Modules.Performance`. Die Seite misst jede Sekunde, aber **nur solange sie geöffnet ist** (`IActivatablePage`: Messung startet beim Öffnen, stoppt beim Verlassen und beim Beenden). Die Seite ist kein `IModule`, weil sie keinen Prüfbericht und keine Aktionen hat, sondern laufend misst.

**Datenquellen** (alle ohne Adminrechte, ohne Treiber, ohne neue Pakete – nur Windows-Funktionen per P/Invoke):
- **CPU:** Leistungsindikatoren (`pdh.dll`) `\Processor Information(*)\% Processor Utility` wie der Task-Manager, je logischem Prozessor; Takt = `Processor Frequency` × `% Processor Performance`.
- **Arbeitsspeicher, Prozesse/Threads/Handles:** `K32GetPerformanceInfo` (kernel32); eingebaut über `GetPhysicallyInstalledSystemMemory`.
- **Laufwerke:** `\PhysicalDisk(*)\% Idle Time` (aktive Zeit = 100 − Leerlauf) sowie Lese-/Schreibrate; Typ und Modell über `MSFT_PhysicalDisk`.
- **Netzwerk:** `System.Net.NetworkInformation` (Bytezähler je Adapter, Differenz pro Sekunde). Virtuelle und getrennte Adapter sieht nur die Expertenansicht. .NET liefert unter Windows auch versteckte Schnittstellen (Filter-Schichten wie „…-WFP … Filter-0000“, WAN-Miniports, Kernel-Debugger) mit doppeltem Datenverkehr; angezeigt werden deshalb nur Adapter, die auch `MSFT_NetAdapter` (wie `Get-NetAdapter`) nennt. Ist diese Liste nicht abrufbar, wird nicht gefiltert.
- **Grafik:** Name, LUID und Grafikspeicher über DXGI (Teil von Windows); Auslastung über `\GPU Engine(*)\Utilization Percentage`: je Engine über alle Programme addiert, angezeigt wird die am stärksten ausgelastete Engine (wie der Task-Manager). Bewusst je Engine statt je Engine-Typ: Zwei Kopier-Engines mit je 60 % sind nicht 120 %. Temperatur als Versuch über D3DKMT (`gdi32.dll`), nur wenn der Treiber einen plausiblen Wert meldet.
- **Feste Werte** (Prozessorname, Kerne, Caches, Speichertakt, Steckplätze): WMI, einmal pro Programmstart.

**Bewertung:** Durchschnitt über 30 Sekunden; ab 90 % bei Prozessor, Arbeitsspeicher oder Laufwerk-Aktivzeit ein **Hinweis** (blau), sonst grün. Kein Gelb oder Rot: Hohe Last ist kein Defekt. Das ist dieselbe Stufe wie früher „Arbeitsspeicher fast voll“ im Gerätezustand. Grafik und Netzwerk werden nicht bewertet (Spiele und Downloads dürfen sie voll auslasten). Vor Ablauf der ersten 30 Sekunden wird nicht bewertet.

**Datenschutz:** IP-Adressen nur in der Expertenansicht anzeigen, nie loggen, speichern oder exportieren.

**Grund:** Werte sollen mit dem Task-Manager übereinstimmen, damit Nutzer der App vertrauen. Keine Fremdbibliothek (LibreHardwareMonitorLib wäre MPL-2.0 mit Kernel-Treiber, siehe 0006).

**Verworfen:** Messung im Hintergrund auch bei geschlossener Seite (unnötige Last); Diagramm-Bibliothek (ein kleines eigenes Steuerelement `UsageGraph` reicht).
