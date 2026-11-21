# Windows-Gegenstück zu publish-windows.sh und publish-agent.sh (für die CI und die Test-VM):
#   artifacts\win-x64\Omnikit\        App + Hilfsprozess      + Omnikit-<Version>-portabel.zip
#   artifacts\win-x64\OmnikitAgent\   Agent                   + OmnikitAgent-<Version>.zip
param([string]$VersionPrefix, [string]$VersionSuffix)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$versionArgs = @()
if ($VersionPrefix) { $versionArgs += "-p:VersionPrefix=$VersionPrefix"; $versionArgs += "-p:VersionSuffix=$VersionSuffix" }

function Publish($project, $out) {
    dotnet publish $project -c Release -r win-x64 --self-contained -p:DebugType=none -o $out @versionArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project ist fehlgeschlagen." }
}

Remove-Item -Recurse -Force artifacts\win-x64 -ErrorAction SilentlyContinue
Publish src\Omnikit.Desktop artifacts\win-x64\Omnikit
Publish src\Omnikit.Helper artifacts\win-x64\Omnikit
Publish src\Omnikit.Agent artifacts\win-x64\OmnikitAgent
Remove-Item artifacts\win-x64\Omnikit\*.pdb -ErrorAction SilentlyContinue

$version = (dotnet msbuild src\Omnikit.Desktop -getProperty:Version @versionArgs).Trim()
Compress-Archive -Path artifacts\win-x64\Omnikit -DestinationPath "artifacts\win-x64\Omnikit-$version-portabel.zip"
Compress-Archive -Path artifacts\win-x64\OmnikitAgent -DestinationPath "artifacts\win-x64\OmnikitAgent-$version.zip"
Get-ChildItem artifacts\win-x64\*.zip | Select-Object Name, Length
