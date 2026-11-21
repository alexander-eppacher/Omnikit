# Omnikit – Erststart
# Wird von der autounattend.xml beim ersten Anmelden gestartet und installiert die im Stick-Builder gewählten
# Programme über winget (offizielle Quelle von Microsoft). Protokoll: C:\Windows\Setup\Scripts\omnikit-erststart.log
param([switch]$TestMode)

$Packages = @(__PACKAGES__)
$Log = if ($TestMode) { Join-Path $PSScriptRoot 'omnikit-erststart.log' } else { 'C:\Windows\Setup\Scripts\omnikit-erststart.log' }

function Write-Log([string]$Text) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Text
    Add-Content -LiteralPath $Log -Value $line -Encoding UTF8
    Write-Host $line
}

$Host.UI.RawUI.WindowTitle = 'Omnikit richtet Programme ein – bitte dieses Fenster nicht schließen'
Write-Log "Erststart beginnt – Programme: $($Packages.Count)"

# 1. Auf Internet warten (höchstens 10 Minuten), z. B. bis das WLAN verbunden ist
$deadline = (Get-Date).AddMinutes(10)
$online = $false
while (-not $online -and (Get-Date) -lt $deadline) {
    $online = Test-NetConnection cdn.winget.microsoft.com -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue
    if (-not $online) { Start-Sleep -Seconds 10 }
}
if (-not $online) { Write-Log 'Keine Internetverbindung – Programme bitte später mit Omnikit installieren.' }

# 2. Auf winget warten: Windows richtet den App-Installer nach der ersten Anmeldung erst ein
$winget = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\winget.exe'
$deadline = (Get-Date).AddMinutes(10)
while ($online -and -not (Test-Path -LiteralPath $winget) -and (Get-Date) -lt $deadline) {
    try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction Stop } catch { }
    Start-Sleep -Seconds 15
}

# 3. Programme nacheinander installieren
if ($online -and (Test-Path -LiteralPath $winget)) {
    foreach ($id in $Packages) {
        Write-Log "Installiere $id …"
        & $winget install --id $id --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity | Out-Null
        Write-Log ('{0}: {1}' -f $id, $(if ($LASTEXITCODE -eq 0) { 'fertig' } else { 'fehlgeschlagen, Code 0x{0:X8}' -f $LASTEXITCODE }))
    }
} elseif ($online) {
    Write-Log 'winget wurde nicht gefunden – Programme bitte später mit Omnikit installieren.'
}

# 4. Automatische Anmeldung beenden: Windows speichert das Passwort dafür sonst lesbar in der Registry
if (-not $TestMode) {
    $winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
    Set-ItemProperty -LiteralPath $winlogon -Name AutoAdminLogon -Value '0'
    Remove-ItemProperty -LiteralPath $winlogon -Name DefaultPassword -ErrorAction SilentlyContinue
}

Write-Log 'Erststart fertig.'
