#!/usr/bin/env bash
# Baut den Agent für Windows (64 Bit) als einzelne Datei, die ohne installiertes .NET läuft.
# Ergebnis: artifacts/agent-win-x64/Omnikit.Agent.exe
set -euo pipefail

cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
OUT="artifacts/agent-win-x64"

rm -rf "$OUT"
"$DOTNET" publish src/Omnikit.Agent \
  -c Release \
  -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none \
  -o "$OUT"

echo
echo "Fertig: $OUT/Omnikit.Agent.exe"
