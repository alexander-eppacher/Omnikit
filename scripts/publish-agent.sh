#!/usr/bin/env bash
# Baut den Agent für Windows (64 Bit) als Ordner, der ohne installiertes .NET läuft:
#   artifacts/win-x64/OmnikitAgent/Omnikit.Agent.exe   (+ .NET-Laufzeit daneben)
#   artifacts/win-x64/OmnikitAgent-<Version>.zip       (für die Einrichtung von Hand: entpacken, register, install)
# Kein Single-File: Das hätte Bibliotheken beim Start in einen Temp-Ordner entpackt (0017).
set -euo pipefail

cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
OUT="artifacts/win-x64/OmnikitAgent"

rm -rf "$OUT"
"$DOTNET" publish src/Omnikit.Agent -c Release -r win-x64 --self-contained -p:DebugType=none -o "$OUT"

VERSION=$("$DOTNET" msbuild src/Omnikit.Agent -getProperty:Version)
ZIP="artifacts/win-x64/OmnikitAgent-$VERSION.zip"
python3 - "$OUT" "$ZIP" <<'PY'
import os, sys, zipfile
folder, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    for root, _, files in os.walk(folder):
        for name in sorted(files):
            path = os.path.join(root, name)
            archive.write(path, os.path.join("OmnikitAgent", os.path.relpath(path, folder)))
PY

echo
echo "Fertig: $OUT/Omnikit.Agent.exe"
echo "ZIP: $ZIP"
