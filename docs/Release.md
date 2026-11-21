# Release erstellen und signieren

Omnikit installiert Updates nur, wenn ihr Manifest mit einem der Schlüssel signiert ist, die in `src/Omnikit.Core/Updates/UpdateSource.cs` (`TrustedKeys`) eingebaut sind. Die privaten Schlüssel liegen **nur offline beim Entwickler**. Sie kommen nie ins Repo, nie zu GitHub und nie in die CI. Auch ein übernommenes GitHub-Konto kann deshalb kein Update verteilen.

## Einmalig: Schlüssel erzeugen

In einem normalen Terminal:

```bash
./scripts/create-signing-keys.sh
```

- Das Skript erzeugt zwei Schlüssel in `~/omnikit-signing/` (`aktiv.pem` zum Signieren, `reserve.pem` für den Notfall). Beide sind mit deinem Passwort verschlüsselt (mindestens 16 Zeichen), und der Ordner ist nur für dich lesbar.
- Es trägt die **öffentlichen** Teile selbst in `src/Omnikit.Core/Updates/UpdateSource.cs` ein. Diese Änderung wird committet, sie ist nicht geheim.
- Vorhandene Schlüssel werden nie überschrieben.
- **Danach:** den Ordner `~/omnikit-signing/` an einen zweiten Ort kopieren, z. B. auf einen USB-Stick im Schrank. Das Passwort getrennt davon aufbewahren, z. B. im Passwort-Manager.

Von Hand geht es auch: `dotnet run --project tools/ReleaseSign -- keygen <datei.pem>` (der öffentliche Teil landet in `<datei.pem>.pub`).

## Bei jedem Release

1. CI grün, `CHANGELOG.md` ergänzt, Version in `Directory.Build.props` (`VersionPrefix`) erhöht.
2. Tag `vX.Y.Z-rc.1` pushen. Die CI legt einen Release-Entwurf an. Diese Dateien auf der VM von Hand abnehmen. Vorabversionen bekommen kein Update-Manifest.
3. Tag `vX.Y.Z` pushen. Die CI legt den Entwurf mit Setups, ZIPs und `SHA256SUMS` an.
4. Im Terminal:
   ```bash
   ./scripts/sign-release.sh vX.Y.Z
   ```
   Das Skript holt die Setups aus dem Entwurf und prüft sie gegen `SHA256SUMS`. Dann schreibt und signiert es beide Manifeste (fragt das Passwort ab), prüft sie mit den eingebauten Schlüsseln und lädt sie zum Entwurf hoch. Mit dem Reserveschlüssel: `OMNIKIT_SIGNING_KEY=~/omnikit-signing/reserve.pem ./scripts/sign-release.sh vX.Y.Z`.
5. Den Entwurf auf GitHub ansehen und veröffentlichen.
6. Sofort auf der VM das Update von der Vorversion ausprobieren.

Einzelschritte von Hand: `tools/ReleaseSign` mit `manifest`, `sign` und `verify` (Aufruf ohne Argumente zeigt die Hilfe).

## Notfälle

- **Ein Update ist kaputt:** Die beiden `omnikit-*-update.json` aus dem Release löschen. Dann sieht kein Gerät mehr ein Update. Das ist der Notschalter. Danach eine Patch-Version mit dem Fix veröffentlichen.
- **Der aktive Schlüssel ist verloren:** Mit dem Reserveschlüssel signieren. Im nächsten Release einen neuen Reserveschlüssel erzeugen und eintragen, den verlorenen austragen.
- **Der aktive Schlüssel wurde gestohlen:** Sofort ein Release mit dem Reserveschlüssel veröffentlichen, das den gestohlenen Schlüssel aus `TrustedKeys` entfernt. Einen neuen Reserveschlüssel erzeugen. Über `SECURITY.md` und die Release-Notizen informieren. Bis alle Geräte aktualisiert sind, akzeptieren alte Installationen den gestohlenen Schlüssel noch. Deshalb müssen die Dateien offline liegen und verschlüsselt sein.

## Was die Signatur nicht schützt

Ein manipulierter Build in der CI würde gutgläubig mitsigniert. Dagegen helfen: Actions per Commit-Hash angeheftet, minimale Rechte der Workflows, keine Geheimnisse für Pull Requests aus Forks und den Diff vor jedem Release selbst ansehen.
