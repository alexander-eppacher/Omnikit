# 0017 – Fahrplan ab Version 4

**Datum:** 11.11.2026

**Entscheidungen:**

1. **Nur noch SQLite.** PostgreSQL wird nicht mehr unterstützt. Die Datenbank ist eine Datei im Volume des Servers, das Schema wird über EF-Core-Migrationen gepflegt (statt `EnsureCreated`).
2. **Kein React-Dashboard.** Die Verwaltung bleibt bei den Razor-Seiten des Servers.
3. **Code-Signing nur kostenlos.** Releases sind zunächst unsigniert; Updates werden über ein eigenes signiertes Manifest geprüft. Eine kostenlose Open-Source-Signatur wird beantragt, ein Zertifikat wird nicht gekauft.
4. **Update-Prüfung** ist als Vorgabe eingeschaltet: höchstens einmal täglich eine Anfrage an die Release-Seite, ohne Gerätekennung, abschaltbar. Das ist der einzige Kontakt nach außen, den Omnikit von sich aus aufnimmt.
5. **Portable und installierte Version** dürfen beide ändern. Der Hilfsprozess mit Adminrechten wird dafür eine eigene kleine Datei ohne Oberflächen-Bibliotheken.
6. **Stick-Builder:** Die Hardware-Prüfung von Windows 11 (TPM, Secure Boot, Prozessor) lässt sich als Option überspringen. Vorgabe aus, mit Warnung.
7. **Gestrichen:** Treiber oder Programme von Drittseiten, Fernwartung/Fernsteuerung, Bezahlmodell, Systemreinigung, Netzwerk-Tools, macOS.

**Grund:** Ein Einzelentwickler kann zwei Datenbanken, zwei Oberflächen-Techniken und mehrere Plattformen nicht dauerhaft gleich gut testen. Weniger Teile heißt weniger Fehler und weniger Angriffsfläche. Vor einer öffentlichen Version braucht es dafür Installer, sichere Updates und ein Schema, das sich weiterentwickeln lässt.

**Reihenfolge:** v4 „Öffentlich“ (Installer, Updates, Migrationen), v5 „Sicherheits-Check“ (neues Modul, nur lesen, Englisch), v6 Benachrichtigungen und Wartungsfenster am Server, v7 Einrichten ohne Anfassen.
