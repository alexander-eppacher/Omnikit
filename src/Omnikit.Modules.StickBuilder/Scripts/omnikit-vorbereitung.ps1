# Omnikit – Vorbereitung
# Läuft während der Windows-Installation (Schritt „specialize“, vor der ersten Anmeldung) mit Systemrechten.
# Entfernt die im Stick-Builder gewählten vorinstallierten Apps und setzt die gewählten Einstellungen.
# Benutzer-Einstellungen kommen in die Vorlage „Default“, aus der Windows jedes neue Konto anlegt.
# Protokoll: C:\Windows\Setup\Scripts\omnikit-vorbereitung.log

$RemoveApps = @(__REMOVEAPPS__)
$PrivacyDefaults = __PRIVACY__
$ShowFileExtensions = __FILEEXTENSIONS__
$TaskbarLeft = __TASKBARLEFT__
$HideWidgets = __HIDEWIDGETS__
$ClassicContextMenu = __CLASSICCONTEXTMENU__
$NoWebSearch = __NOWEBSEARCH__
$PreventDeviceEncryption = __PREVENTENCRYPTION__
$DisableFastStartup = __DISABLEFASTSTARTUP__
$PasswordNeverExpires = __PASSWORDNEVEREXPIRES__
$Log = 'C:\Windows\Setup\Scripts\omnikit-vorbereitung.log'

function Write-Log([string]$Text) {
    Add-Content -LiteralPath $Log -Value ('{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Text) -Encoding UTF8
}

function Set-Value([string]$Path, [string]$Name, [int]$Value) {
    New-Item -Path $Path -Force | Out-Null
    Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type DWord
}

Write-Log 'Vorbereitung beginnt'

# 1. Vorinstallierte Apps entfernen – für alle künftigen Benutzer („provisioned“)
$provisioned = Get-AppxProvisionedPackage -Online
foreach ($name in $RemoveApps) {
    foreach ($package in @($provisioned | Where-Object DisplayName -eq $name)) {
        try {
            Remove-AppxProvisionedPackage -Online -PackageName $package.PackageName -ErrorAction Stop | Out-Null
            Write-Log "Entfernt: $name"
        } catch {
            Write-Log "Nicht entfernt: $name ($($_.Exception.Message))"
        }
    }
}

# 2. Einstellungen für den ganzen PC
if ($PrivacyDefaults) {
    # Diagnosedaten nur das Nötigste („Erforderlich“)
    Set-Value 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry' 1
    # Werbe-ID für Apps aus
    Set-Value 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo' 'DisabledByGroupPolicy' 1
    Write-Log 'Datenschutz-Grundeinstellungen gesetzt'
}
if ($HideWidgets) {
    Set-Value 'HKLM:\SOFTWARE\Policies\Microsoft\Dsh' 'AllowNewsAndInterests' 0
    Write-Log 'Widgets ausgeblendet'
}
if ($PreventDeviceEncryption) {
    # Muss vor der ersten Anmeldung gesetzt sein, sonst hat Windows schon zu verschlüsseln begonnen
    Set-Value 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption' 1
    Write-Log 'Automatische Geräteverschlüsselung aus'
}
if ($DisableFastStartup) {
    Set-Value 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 0
    Write-Log 'Schnellstart aus'
}
if ($PasswordNeverExpires) {
    & net.exe accounts /maxpwage:UNLIMITED | Out-Null
    Write-Log 'Passwörter laufen nicht ab'
}

# 3. WLAN eintragen (für alle Benutzer), damit der PC nach dem Setup ohne Klick ins Internet kommt
$WifiProfile = 'C:\Windows\Setup\Scripts\omnikit-wlan.xml'
if (Test-Path -LiteralPath $WifiProfile) {
    $output = & netsh.exe wlan add profile filename="$WifiProfile" user=all
    Write-Log "WLAN eintragen: $(($output | Where-Object { $_ }) -join ' ') (Code $LASTEXITCODE)"
    Remove-Item -LiteralPath $WifiProfile -Force
    # Das Passwort steht auch in der Kopie der Antwortdatei, die Windows aufbewahrt: dort herausnehmen
    foreach ($copy in Get-ChildItem -Path 'C:\Windows\Panther' -Filter 'unattend*.xml' -File -ErrorAction SilentlyContinue) {
        $xml = [xml](Get-Content -LiteralPath $copy.FullName -Raw -Encoding UTF8)
        foreach ($file in @($xml.unattend.Extensions.File | Where-Object path -eq $WifiProfile)) {
            [void]$file.ParentNode.RemoveChild($file)
            $xml.Save($copy.FullName)
            Write-Log "WLAN-Passwort aus $($copy.Name) entfernt"
        }
    }
}

# 4. Einstellungen für jeden neuen Benutzer (Vorlage „Default“)
if ($PrivacyDefaults -or $ShowFileExtensions -or $TaskbarLeft -or $ClassicContextMenu -or $NoWebSearch) {
    & reg.exe load 'HKU\OmnikitDefault' 'C:\Users\Default\NTUSER.DAT' | Out-Null
    try {
        $user = 'Registry::HKEY_USERS\OmnikitDefault\Software'
        $advanced = "$user\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
        if ($PrivacyDefaults) {
            # Keine still nachinstallierten Werbe-Apps und Vorschläge
            foreach ($name in 'SilentInstalledAppsEnabled', 'PreInstalledAppsEnabled', 'OemPreInstalledAppsEnabled', 'SystemPaneSuggestionsEnabled') {
                Set-Value "$user\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" $name 0
            }
            Set-Value "$user\Microsoft\Windows\CurrentVersion\Privacy" 'TailoredExperiencesWithDiagnosticDataEnabled' 0
            # Keine Seite „Lass uns die Einrichtung abschließen“ nach Updates
            Set-Value "$user\Microsoft\Windows\CurrentVersion\UserProfileEngagement" 'ScoobeSystemSettingEnabled' 0
        }
        if ($ShowFileExtensions) {
            Set-Value $advanced 'HideFileExt' 0
        }
        if ($TaskbarLeft) {
            Set-Value $advanced 'TaskbarAl' 0
        }
        if ($NoWebSearch) {
            Set-Value "$user\Policies\Microsoft\Windows\Explorer" 'DisableSearchBoxSuggestions' 1
        }
        if ($ClassicContextMenu) {
            # Der Schlüssel liegt in „HKCU\Software\Classes“, das Windows erst bei der ersten Anmeldung anlegt:
            # deshalb einmalig beim ersten Anmelden setzen
            $runOnce = "$user\Microsoft\Windows\CurrentVersion\RunOnce"
            New-Item -Path $runOnce -Force | Out-Null
            Set-ItemProperty -Path $runOnce -Name 'OmnikitContextMenu' -Type String `
                -Value 'reg.exe add "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" /ve /f'
        }
        Write-Log 'Benutzer-Einstellungen gesetzt'
    } finally {
        [GC]::Collect()
        & reg.exe unload 'HKU\OmnikitDefault' | Out-Null
    }
}

Write-Log 'Vorbereitung fertig'
