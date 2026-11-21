# Stellt Inno Setup 6 bereit (für die CI). Ist es schon installiert, passiert nichts.
# Quelle: das winget-Paket des Herstellers (Prüfsumme wird von winget geprüft), sonst Chocolatey mit fester Version.
$ErrorActionPreference = 'Stop'
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) { "Inno Setup ist da: $iscc"; return }

if (Get-Command winget -ErrorAction SilentlyContinue) {
    winget install --id JRSoftware.InnoSetup -e --version 6.7.3 --silent --accept-package-agreements --accept-source-agreements --scope machine
} else {
    choco install innosetup --version=6.7.3 -y --no-progress
}
if (-not (Test-Path $iscc)) { throw "Inno Setup konnte nicht installiert werden." }
