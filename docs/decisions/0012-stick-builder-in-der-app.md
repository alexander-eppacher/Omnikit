# 0012 – Stick-Builder in der Desktop-App

**Datum:** 31.10.2026

**Entscheidung:** Der Stick-Builder (Phase 3) wird eine **Seite in Omnikit** statt einer eigenen Webapp. Die Logik (Antwortdatei, Erststart-Skript, ZIP) liegt in der Bibliothek `Omnikit.Modules.StickBuilder` ohne Oberfläche.

- Gleiche Sprache (C#) und gleicher Programmkatalog wie die Seite „Apps“ – keine zweite Liste, die auseinanderlaufen kann.
- In der App kann Omnikit die `autounattend.xml` nach Bestätigung direkt auf einen eingesteckten Windows-Installationsstick schreiben. Alternativ als ZIP speichern.
- Weiterhin gilt: kein Windows-Image verteilen, keine Nutzerdaten speichern; die Antwortdatei entsteht nur lokal.
- Eine Webversion bleibt möglich (z. B. Blazor WebAssembly mit derselben Bibliothek), ist aber nicht geplant.

**Grund:** Wunsch des Entwicklers („will diese Stick-Builder in meiner app drinnen haben“). Außerdem hätte die React-Webapp eine eigene npm-Werkzeugkette gebraucht.

**Generische Schlüssel:** Damit das Setup die Edition ohne Rückfrage wählt, trägt der Stick-Builder die öffentlichen generischen Installationsschlüssel von Microsoft ein (z. B. Pro `VK7JG-NPHTM-C97JM-9MPGT-3V66T`). Sie aktivieren Windows **nicht**; aktiviert wird wie gewohnt über die digitale Lizenz des Geräts oder einen eigenen Schlüssel.

**Vorbild:** Aufbau der Antwortdatei angelehnt an Christoph Schneegans' Unattend-Generator (MIT-Lizenz, schneegans.de); es wurde kein Code übernommen.
