# 0002 – Plattform: Ziel Windows, unter Linux startfähig

**Datum:** 24.10.2026

**Entscheidung:** Alle Funktionen zielen auf Windows. Die App startet aber auch unter Linux und zeigt Windows-spezifische Werte dort als „nicht verfügbar“ an. Linux-Unterstützung im eigentlichen Sinn (Werte unter Linux auslesen) bleibt ausgeklammert (optionale Erweiterung für später).

**Grund:** Entwickelt wird in einer Linux-VM. So kann die Oberfläche lokal gestartet werden. Der Mehraufwand ist gering, weil Hardwarezugriffe ohnehin hinter Interfaces liegen.

**Folge:** Die Projekte zielen auf `net10.0` (nicht `net10.0-windows`). Windows-Code wird mit `OperatingSystem.IsWindows()` abgesichert.
