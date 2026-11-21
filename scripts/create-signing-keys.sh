#!/usr/bin/env bash
# Einmalig: erzeugt die beiden Signierschlüssel für Updates (aktiv + Reserve) und trägt ihre öffentlichen Teile
# in src/Omnikit.Core/Updates/UpdateSource.cs ein. Die privaten Schlüssel bleiben verschlüsselt in einem Ordner
# nur für dich (Standard: ~/omnikit-signing) und kommen nie ins Repo. Siehe docs/Release.md.
#
# Aufruf in einem normalen Terminal (das Passwort wird abgefragt):  ./scripts/create-signing-keys.sh [ordner]
set -euo pipefail

cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
KEYDIR="${1:-$HOME/omnikit-signing}"
SOURCE="src/Omnikit.Core/Updates/UpdateSource.cs"

if [[ -e "$KEYDIR/aktiv.pem" || -e "$KEYDIR/reserve.pem" ]]; then
  echo "In $KEYDIR liegen schon Schlüssel – es wird nichts überschrieben." >&2
  exit 1
fi
if ! grep -q 'public static IReadOnlyList<byte\[\]> TrustedKeys { get; } = \[\];' "$SOURCE"; then
  echo "In $SOURCE sind schon Schlüssel eingetragen – für einen Schlüsselwechsel siehe docs/Release.md." >&2
  exit 1
fi
if [[ ! -t 0 ]]; then
  echo "Bitte in einem normalen Terminal starten (das Passwort wird abgefragt)." >&2
  exit 1
fi

mkdir -p "$KEYDIR"
chmod 700 "$KEYDIR"

echo "Ein Passwort mit mindestens 16 Zeichen wählen. Es wird für jeden Schlüssel abgefragt (dasselbe ist in Ordnung)."
echo "Ohne Passwort lässt sich kein Update mehr signieren – sicher aufbewahren (z. B. Passwort-Manager)."
echo
for name in aktiv reserve; do
  echo "== Schlüssel „$name“"
  "$DOTNET" run --project tools/ReleaseSign -v quiet -- keygen "$KEYDIR/$name.pem"
  chmod 600 "$KEYDIR/$name.pem"
  echo
done

ACTIVE=$(cat "$KEYDIR/aktiv.pem.pub")
RESERVE=$(cat "$KEYDIR/reserve.pem.pub")
python3 - "$SOURCE" "$ACTIVE" "$RESERVE" <<'PY'
import sys
path, active, reserve = sys.argv[1:4]
text = open(path, encoding="utf-8").read()
old = "    public static IReadOnlyList<byte[]> TrustedKeys { get; } = [];"
new = (
    "    public static IReadOnlyList<byte[]> TrustedKeys { get; } =\n"
    "    [\n"
    "        // aktiv\n"
    f'        Convert.FromBase64String("{active}"),\n'
    "        // Reserve (nur für den Notfall, siehe docs/Release.md)\n"
    f'        Convert.FromBase64String("{reserve}"),\n'
    "    ];"
)
assert text.count(old) == 1
open(path, "w", encoding="utf-8").write(text.replace(old, new))
PY

echo "Fertig."
echo "  Private Schlüssel: $KEYDIR/aktiv.pem und $KEYDIR/reserve.pem (verschlüsselt)"
echo "  Öffentliche Teile sind in $SOURCE eingetragen – diese Änderung committen."
echo
echo "Jetzt noch: den Ordner $KEYDIR an einen ZWEITEN Ort kopieren (z. B. USB-Stick im Schrank)."
echo "Ohne Schlüssel kann niemand mehr Updates verteilen, auch du nicht."
