#!/usr/bin/env bash
# Baut Omnikit für Windows (64 Bit) als Ordner, der ohne installiertes .NET läuft:
#   artifacts/win-x64/Omnikit/Omnikit.exe          – die App (startet ohne Adminrechte)
#   artifacts/win-x64/Omnikit/Omnikit.Helper.exe   – der Hilfsprozess für Aktionen mit Adminrechten (0005, 0017)
# Beide teilen sich die mitgelieferte .NET-Laufzeit im selben Ordner. Daraus entsteht auch die portable Version als ZIP.
# Kein Single-File mehr: Das hätte Bibliotheken beim Start in einen Benutzerordner (%TEMP%) entpackt.
set -euo pipefail

cd "$(dirname "$0")/.."
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
OUT="artifacts/win-x64/Omnikit"
COMMON=(-c Release -r win-x64 --self-contained -p:DebugType=none -o "$OUT")

rm -rf artifacts/win-x64/Omnikit artifacts/win-x64/Omnikit-*.zip
"$DOTNET" publish src/Omnikit.Desktop "${COMMON[@]}"
"$DOTNET" publish src/Omnikit.Helper "${COMMON[@]}"

# Debug-Symbole aus den Grafik-Bibliotheken werden auf dem Ziel-PC nicht gebraucht.
rm -f "$OUT"/*.pdb

VERSION=$("$DOTNET" msbuild src/Omnikit.Desktop -getProperty:Version)
ZIP="artifacts/win-x64/Omnikit-$VERSION-portabel.zip"
python3 - "$OUT" "$ZIP" <<'EOF'
import os, sys, zipfile
folder, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
    for root, _, files in os.walk(folder):
        for name in sorted(files):
            path = os.path.join(root, name)
            archive.write(path, os.path.join("Omnikit", os.path.relpath(path, folder)))
EOF

echo
echo "Fertig: $OUT/Omnikit.exe (+ Omnikit.Helper.exe)"
echo "Portabel: $ZIP"
