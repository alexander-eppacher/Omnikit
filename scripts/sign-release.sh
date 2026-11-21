#!/usr/bin/env bash
# Bei jedem Release: holt die Setups aus dem Release-Entwurf (GitHub), prüft sie gegen SHA256SUMS, schreibt und signiert
# die Update-Manifeste mit deinem Schlüssel, prüft sie mit den in Omnikit eingebauten Schlüsseln und lädt sie zum Entwurf hoch.
# Veröffentlicht wird danach von Hand auf GitHub. Siehe docs/Release.md.
#
# Aufruf in einem normalen Terminal (das Passwort wird abgefragt):  ./scripts/sign-release.sh v4.0.1
# Anderer Schlüssel (z. B. Reserve):  OMNIKIT_SIGNING_KEY=~/omnikit-signing/reserve.pem ./scripts/sign-release.sh v4.0.1
set -euo pipefail

cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
KEY="${OMNIKIT_SIGNING_KEY:-$HOME/omnikit-signing/aktiv.pem}"
TAG="${1:-}"

if [[ ! "$TAG" =~ ^v([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
  echo "Aufruf: $0 vX.Y.Z  (Vorabversionen bekommen kein Update-Manifest)" >&2
  exit 1
fi
VERSION="${BASH_REMATCH[1]}"
[[ -f "$KEY" ]] || { echo "Schlüssel fehlt: $KEY (einmalig scripts/create-signing-keys.sh)" >&2; exit 1; }
[[ -t 0 ]] || { echo "Bitte in einem normalen Terminal starten (das Passwort wird abgefragt)." >&2; exit 1; }

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

echo "== Dateien aus dem Release-Entwurf $TAG holen"
gh release download "$TAG" -D "$WORK" -p "Omnikit-$VERSION-setup.exe" -p "OmnikitAgent-$VERSION-setup.exe" -p SHA256SUMS
(cd "$WORK" && sha256sum --check --ignore-missing SHA256SUMS)

SIGN=("$DOTNET" run --project tools/ReleaseSign -v quiet --)
for product in app agent; do
  if [[ $product == app ]]; then setup="Omnikit-$VERSION-setup.exe"; else setup="OmnikitAgent-$VERSION-setup.exe"; fi
  manifest="$WORK/omnikit-$product-update.json"
  echo "== $product: Manifest schreiben und signieren"
  "${SIGN[@]}" manifest "$product" "$VERSION" "$WORK/$setup" "$manifest"
  "${SIGN[@]}" sign "$KEY" "$manifest"
  "${SIGN[@]}" verify "$manifest" "$product"
done

echo "== Manifeste und Signaturen zum Entwurf hochladen"
gh release upload "$TAG" --clobber "$WORK"/omnikit-*-update.json "$WORK"/omnikit-*-update.json.sig

echo
echo "Fertig. Jetzt auf GitHub den Entwurf $TAG ansehen und veröffentlichen."
echo "Danach auf der VM das Update von der Vorversion ausprobieren."
