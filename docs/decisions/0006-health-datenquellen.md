# 0006 – Datenquellen des Health-Moduls

**Datum:** 24.10.2026

**Entscheidung:**
- **Laufwerke:** Windows-Storage-API (`MSFT_PhysicalDisk`, `MSFT_StorageReliabilityCounter`) statt smartmontools.
- **Akku:** `powercfg /batteryreport /xml` (keine Adminrechte nötig).
- **CPU-/GPU-Temperaturen:** vorerst nicht. Nur Auslastung: CPU über den Leistungsindikator „% Processor Utility“ (wie der Task-Manager, Nachtrag 24.10.2026: `Win32_Processor.LoadPercentage` wich zu stark ab und ist nur noch Ersatz), RAM über WMI plus eingebauter Speicher über `GetPhysicallyInstalledSystemMemory`.
- **Nachtrag 30.10.2026:** Der Bereich „Auslastung“ ist aus dem Gerätezustand entfernt und auf die Seite „Leistung“ umgezogen (siehe 0008). Eine einzelne Momentaufnahme war wenig aussagekräftig; die neue Seite misst laufend und bewertet den Durchschnitt über 30 Sekunden.
- **Updates:** Windows-Update-COM-Schnittstelle, nur suchen, mit 60 s Zeitlimit.

**Grund:** Keine Fremdsoftware und keine GPL/MPL-Abhängigkeit (smartmontools: GPL-2.0; LibreHardwareMonitorLib: MPL-2.0 plus Kernel-Treiber). Nachteil: Die geschriebene Datenmenge (TBW) fehlt oft. Stattdessen wird der Verschleißwert genutzt, den die SSD selbst meldet und den der Hersteller aus seiner TBW-Angabe berechnet.

**Grenzwerte der Ampel:** stehen als Konstanten in den `*Evaluator`-Klassen und sind durch Tests abgedeckt.
