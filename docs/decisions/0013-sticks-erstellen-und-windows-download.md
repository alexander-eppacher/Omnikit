# 0013 – USB-Sticks erstellen und Windows von Microsoft laden

**Datum:** 31.10.2026

**Entscheidung:** Die Seite „Windows-Stick“ erstellt bootfähige USB-Sticks wie Rufus – aus einer eigenen ISO oder aus Windows 11, das Omnikit selbst von Microsoft lädt. Danach kann eine automatische Installation eingerichtet (eigene Optionen) oder eine vorhandene `autounattend.xml` importiert werden.

**Sticks erstellen (ändert und löscht – nur nach Bestätigung, im Admin-Hilfsprozess):**
- Der Hilfsprozess (0005) nimmt jetzt Parameter an, meldet Fortschritt über die Pipe (eine JSON-Zeile je Nachricht) und hat eine Zeitgrenze je Aktion. Parameter prüft jede Aktion selbst.
- Gelöscht wird nur ein Datenträger mit BusType USB, der weder System- noch Startlaufwerk ist und höchstens 256 GB hat. Die vom Nutzer bestätigte Größe muss exakt passen (Datenträgernummern ändern sich beim Umstecken). Diese Prüfung läuft im Hilfsprozess, nicht nur in der App.
- Windows: MBR, eine FAT32-Partition (höchstens 32 GB, Grenze von Windows' Formatierung), Dateien kopieren, `install.wim` über 4 GB mit DISM in `.swm` teilen. Startet auf UEFI-PCs (Windows 11 setzt UEFI voraus).
- Andere ISOs: 1:1 auf den Datenträger („DD-Modus“); Hinweis, dass nur hybride ISOs so starten.
- PowerShell-Befehle per `-EncodedCommand` statt Skriptdatei (keine austauschbare Datei in einem beschreibbaren Ordner); Werte nur als geprüfte Zahlen oder sicher gequotete Pfade.

**Windows von Microsoft laden:**
- Quelle ist der Katalog des Media Creation Tool (`go.microsoft.com/fwlink/?LinkId=2156292` → `products.cab` auf download.microsoft.com, per HTTPS). Er nennt je Sprache die offizielle ESD-Datei mit Größe und SHA-1.
- Die ESD-Dateien liefert Microsoft nur per HTTP (`dl.delivery.mp.microsoft.com`). Deshalb: nur Microsoft-Server zulassen, SHA-1 nach dem Download prüfen und **im Hilfsprozess noch einmal**, bevor gelöscht wird (die Datei liegt im Benutzerprofil, das andere Programme des Nutzers verändern könnten). SHA-1 ist gegen Kollisionen schwach, eine passende Fälschung zu einer vorgegebenen Datei ist aber praktisch nicht machbar – und es ist die einzige Prüfsumme, die Microsoft liefert.
- Aus der ESD baut Omnikit mit DISM einen Installationsordner unter `C:\Windows\Temp` (nur für Administratoren beschreibbar): Abbild 1 = Setup-Dateien, 2 und 3 = `boot.wim` (Setup startfähig), ab 4 = Editionen in `install.wim`. Danach wie bei einer ISO auf den Stick.
- Geladene Dateien bleiben in `%LOCALAPPDATA%\Omnikit\downloads`; ein abgebrochener Download wird fortgesetzt.

**Verworfen:** Auslesen der Microsoft-Download-Webseite (wie „Fido“): bricht, sobald Microsoft die Seite ändert, und wird teils blockiert. UEFI:NTFS-Bootloader von Rufus: fremder Code (GPL), nicht nötig dank geteilter `.swm`.
