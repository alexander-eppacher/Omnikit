# Baut beide Setups mit Inno Setup 6 (nur unter Windows, z. B. in der CI oder auf der Test-VM).
# Vorher die Programmordner bauen (Linux: scripts/publish-windows.sh und scripts/publish-agent.sh,
# Windows: dieselben dotnet-publish-Aufrufe). Ergebnis: artifacts\installer\Omnikit-<Version>-setup.exe und OmnikitAgent-<Version>-setup.exe
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version wie 4.0.0 angeben (ohne Zusatz)." }
if (-not (Test-Path $Iscc)) { throw "Inno Setup 6 nicht gefunden: $Iscc" }

$root = Split-Path -Parent $PSScriptRoot
foreach ($script in 'Omnikit.iss', 'OmnikitAgent.iss') {
    & $Iscc "/DAppVersion=$Version" (Join-Path $root "installer\$script")
    if ($LASTEXITCODE -ne 0) { throw "ISCC ist bei $script fehlgeschlagen ($LASTEXITCODE)." }
}
Get-ChildItem (Join-Path $root 'artifacts\installer') | Select-Object Name, Length
