# 0014 – Treiber-Erkennung

**Datum:** 02.11.2026

**Entscheidung:** Die Seite „Treiber“ ist ein Modul wie „Gerätezustand“ (`IModule`) und **liest nur**. Omnikit installiert, aktualisiert oder deaktiviert keine Treiber.

- **Daten:** WMI `Win32_PnPEntity` (Fehlercode des Geräte-Managers, Klasse, Geräte-ID, angeschlossen ja/nein) und `Win32_PnPSignedDriver` (Anbieter, Version, Datum, INF-Datei). Beides geht ohne Adminrechte und dauert unter einer Sekunde.
- **Bewertung:** Fehlercodes nach der Microsoft-Dokumentation „Device Manager error messages“ in Gruppen mit je einer Abhilfe (kein Treiber, Treiber beschädigt, blockiert, startet nicht, Ressourcen, Neustart nötig). Gelb für echte Fehler; „Neustart nötig“ und ausgeschaltete Geräte (Code 22) nur als Hinweis. Nicht angeschlossene Geräte (Code 45) zählen nicht.
- **Notfall-Grafiktreiber** (`display.inf`, „Microsoft Basic Display Adapter“): auf echten PCs gelb, in VMs nur Hinweis.
- **Expertenansicht:** Treiber der wichtigen Geräte (Grafik, Netzwerk, Audio, Bluetooth, Kamera, Scanner, Speicher-Controller); Software-Geräte (`ROOT\`, `SWD\`, z. B. VPN-Adapter) ausgeblendet.
- **Aktionen öffnen nur Windows-Fenster:** „Optionale Updates öffnen“ (`ms-settings:windowsupdate-optionalupdates`, nur bei Problemen; dort zeigt Windows Treiber von Microsoft und den Herstellern, der Nutzer wählt selbst) und „Geräte-Manager öffnen“ (`devmgmt.msc`). Auf der VM geprüft: Der Geräte-Manager startet ohne Adminrechte; für Änderungen fragt Windows selbst.

**Bewusst nicht:**
- Keine Treiber-Downloads, keine Treiberdatenbanken von Drittanbietern (Regel: keine Downloads von Drittseiten).
- Kein „Treiber veraltet“ nach Datum: Hersteller-Treiber sind oft absichtlich alt und stabil; ein Datum allein sagt nichts über Probleme.

**Datenschutz:** Geräte-IDs können bei USB-Geräten eine Seriennummer enthalten. Sie stehen nur in der Expertenansicht und im Export, nie im Log.
