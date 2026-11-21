; Setup der Desktop-App (Inno Setup 6). Bauen: scripts/build-installers.ps1 (auf Windows bzw. in der CI).
; Installiert immer nach „C:\Program Files\Omnikit“ (fester Ort: nur Administratoren können dort Dateien austauschen,
; und nur dort aktualisiert sich Omnikit selbst). Kein eigener Code mit Adminrechten außer diesem Skript.

#ifndef AppVersion
  #error "AppVersion fehlt, z. B. ISCC /DAppVersion=4.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64\Omnikit"
#endif

[Setup]
AppId={{6B2C8F0E-4E1D-4C8B-9A57-0F3D2E8B7A41}
AppName=Omnikit
AppVersion={#AppVersion}
AppVerName=Omnikit {#AppVersion}
AppPublisher=Alexander Eppacher
AppPublisherURL=https://github.com/alexander-eppacher/Omnikit
AppSupportURL=https://github.com/alexander-eppacher/Omnikit/issues
DefaultDirName={commonpf64}\Omnikit
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=Omnikit-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Omnikit
UninstallDisplayIcon={app}\Omnikit.exe
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; Dateien einer älteren Version entfernen, damit keine veralteten Bibliotheken liegen bleiben
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Omnikit"; Filename: "{app}\Omnikit.exe"

[Run]
; „Jetzt starten“ als der Benutzer, der das Setup gestartet hat – nie mit den Adminrechten des Setups
Filename: "{app}\Omnikit.exe"; Description: "Omnikit starten"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
; Eigene, geschützte Ordner der App: Windows-Medien (mehrere GB) und geladene Updates
Type: filesandordirs; Name: "{commonpf64}\Omnikit Media"
Type: filesandordirs; Name: "{commonpf64}\Omnikit Updates"
