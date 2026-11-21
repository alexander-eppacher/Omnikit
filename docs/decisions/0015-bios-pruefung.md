# 0015 – BIOS-Prüfung

**Datum:** 02.11.2026

**Entscheidung:** Die Seite „BIOS“ ist ein Modul (`IModule`) und **liest nur**. Omnikit flasht nie ein BIOS und lädt keine Firmware – auch nicht in späteren Versionen ohne neue ausdrückliche Entscheidung des Entwicklers.

- **Daten (ohne Adminrechte):** `Win32_BIOS` (Hersteller, `SMBIOSBIOSVersion`, Datum), `Win32_BaseBoard` (Hauptplatine), Startmodus über die Windows-Funktion `GetFirmwareType`, Secure Boot aus der Registry (`HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State`, `UEFISecureBootEnabled`). Die BIOS-Seriennummer wird nicht abgefragt.
- **Bewertung:** Secure Boot aus = gelb (in VMs Hinweis). Altes BIOS („Legacy“) und BIOS älter als 3 Jahre = nur Hinweis – ein Datum allein ist kein Fehler.
- **Kein Versionsvergleich:** Es gibt keine zuverlässige, offizielle Schnittstelle der Hersteller für „neueste BIOS-Version“. Statt zu raten, verlinkt Omnikit die offizielle Support-Seite (Lenovo, HP, Dell, ASUS, Acer, MSI, Gigabyte, ASRock, Microsoft Surface). Bei selbst gebauten PCs zählt der Mainboard-Hersteller. Unbekannte Hersteller bekommen einen allgemeinen Hinweis ohne Link.
- **In VMs** kein Update-Hinweis: Das BIOS kommt vom Virtualisierer.
