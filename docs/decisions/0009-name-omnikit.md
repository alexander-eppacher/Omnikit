# 0009 – Name „Omnikit“

**Datum:** 31.10.2026

**Entscheidung:** Die App heißt **Omnikit** (vorher Arbeitstitel „PcTool“). Umbenannt wurde alles: Anzeigename, `Omnikit.exe`, Projekte und Namespaces (`Omnikit.Core`, `Omnikit.Modules.*`, `Omnikit.Desktop`), Datenordner, Log-Dateien und das GitHub-Repository.

**Grund:** Ein eigener Name lässt sich vermarkten. „Omnikit“ bedeutet „ein Werkzeugkasten für alles“ (lat. *omni* = alles, engl. *kit* = Werkzeugkasten) und passt zum Ziel „alles in einem Tool“. Der Name ist englisch und funktioniert damit auch in der geplanten italienischen und englischen Oberfläche. Eine kurze Websuche fand keine Software mit diesem Namen; eine Markenprüfung steht vor einer Veröffentlichung noch aus.

**Folgen:** Einstellungen liegen jetzt in `%APPDATA%\Omnikit\`. Beim ersten Start wird eine vorhandene `settings.json` aus dem alten Ordner `PcTool` einmalig kopiert (nie gelöscht, nie überschrieben). Alte Log-Dateien bleiben im alten Ordner und werden nicht mehr fortgeschrieben.
