#!/usr/bin/env bash
# Baut Omnikit.exe für Windows (64 Bit) als einzelne Datei, die ohne installiertes .NET läuft.
# Ergebnis: artifacts/win-x64/Omnikit.exe
set -euo pipefail

cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
OUT="artifacts/win-x64"

rm -rf "$OUT"
"$DOTNET" publish src/Omnikit.Desktop \
  -c Release \
  -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none \
  -o "$OUT"

# Debug-Symbole aus den Grafik-Bibliotheken werden auf dem Ziel-PC nicht gebraucht.
rm -f "$OUT"/*.pdb

echo
echo "Fertig: $OUT/Omnikit.exe"
