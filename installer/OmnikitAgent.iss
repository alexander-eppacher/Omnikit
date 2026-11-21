; Setup des Agents (Windows-Dienst, Inno Setup 6). Bauen: scripts/build-installers.ps1.
; Still einrichten:  OmnikitAgent-4.0.0-setup.exe /VERYSILENT /SERVER=https://omnikit.example/ /CODE=ABCD-EFGH [/CA=C:\pfad\root.crt]
; Ohne /SERVER und /CODE werden nur die Dateien installiert bzw. aktualisiert; ein schon registrierter Agent läuft danach weiter.

#ifndef AppVersion
  #error "AppVersion fehlt, z. B. ISCC /DAppVersion=4.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64\OmnikitAgent"
#endif

[Setup]
AppId={{C41E7A2B-8D35-4F6A-B0E9-5A7C1D3E9F28}
AppName=Omnikit Agent
AppVersion={#AppVersion}
AppVerName=Omnikit Agent {#AppVersion}
AppPublisher=Alexander Eppacher
AppPublisherURL=https://github.com/alexander-eppacher/Omnikit
DefaultDirName={commonpf64}\Omnikit Agent
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=OmnikitAgent-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Omnikit Agent
; Der Dienst wird vor dem Kopieren angehalten (PrepareToInstall), nicht über den Neustart-Manager
CloseApplications=no

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; Alte Programmdateien weg – Einstellungen und Token in „config“ bleiben
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.json"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Run]
; Registrieren nur, wenn Server und Code mitgegeben wurden; die Werte gehen als einzelne Argumente an den Agent
Filename: "{app}\Omnikit.Agent.exe"; Parameters: "{code:RegisterParameters}"; Flags: runhidden waituntilterminated; Check: ShouldRegister
; Dienst einrichten bzw. wieder starten – nur, wenn der Agent registriert ist
Filename: "{app}\Omnikit.Agent.exe"; Parameters: "install"; Flags: runhidden waituntilterminated; Check: IsRegistered

[UninstallRun]
; Dienst entfernen und Einstellungen samt Token löschen
Filename: "{app}\Omnikit.Agent.exe"; Parameters: "uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveService"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\updates"

[Code]
function ShouldRegister: Boolean;
begin
  Result := (ExpandConstant('{param:SERVER}') <> '') and (ExpandConstant('{param:CODE}') <> '');
end;

function IsRegistered: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\config\agent.json'));
end;

// Anführungszeichen und Backslashes am Ende sind nicht erlaubt: Beides könnte das Ende des Werts verschieben
function Quoted(Value: String): String;
begin
  StringChangeEx(Value, '"', '', True);
  while (Length(Value) > 0) and (Value[Length(Value)] = '\') do
    Delete(Value, Length(Value), 1);
  Result := '"' + Value + '"';
end;

function RegisterParameters(Param: String): String;
begin
  Result := 'register --server ' + Quoted(ExpandConstant('{param:SERVER}')) + ' --code ' + Quoted(ExpandConstant('{param:CODE}'));
  if ExpandConstant('{param:CA}') <> '' then
    Result := Result + ' --ca ' + Quoted(ExpandConstant('{param:CA}'));
end;

// Den laufenden Dienst anhalten, damit seine Dateien ersetzt werden können (auch beim Selbst-Update)
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code, Tries: Integer;
begin
  Result := '';
  // Gibt es den Dienst noch nicht (Erstinstallation), ist nichts zu tun
  if not Exec(ExpandConstant('{sys}\sc.exe'), 'query OmnikitAgent', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
    Exit;

  Exec(ExpandConstant('{sys}\sc.exe'), 'stop OmnikitAgent', '', SW_HIDE, ewWaitUntilTerminated, Code);
  // höchstens 30 Sekunden warten, bis er wirklich steht
  for Tries := 1 to 30 do
  begin
    Exec(ExpandConstant('{cmd}'), '/c sc.exe query OmnikitAgent | find "STOPPED" >nul', '', SW_HIDE, ewWaitUntilTerminated, Code);
    if Code = 0 then
      Exit;
    Sleep(1000);
  end;
end;
