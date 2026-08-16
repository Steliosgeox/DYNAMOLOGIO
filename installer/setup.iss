; Inno Setup Script for ΔΥΝΑΜΟΛΟΓΙΟ (Production Offline Installer)
; Minimum OS: Windows 7 SP1 (x86 & x64), Windows 10, Windows 11

#define MyAppName "ΔΥΝΑΜΟΛΟΓΙΟ"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Hellenic Armed Forces Administrative Solutions"
#define MyAppExeName "Dynamologio.exe"

[Setup]
AppId={{C8E1B64E-9A63-4DC2-87F9-5C32E9D472F1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Dynamologio
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\installer_output
OutputBaseFilename=Dynamologio_Setup_v1.0.0_Offline
Compression=lzma2/max
SolidCompression=yes
MinVersion=6.1sp1
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=auto
WizardStyle=modern

[Languages]
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Application Binaries
Source: "..\src\Dynamologio.App\bin\Debug\net472\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Protected writable program data directories
Name: "{commonappdata}\Dynamologio\Data"; Permissions: authusers-modify
Name: "{commonappdata}\Dynamologio\Backups"; Permissions: authusers-modify
Name: "{commonappdata}\Dynamologio\Templates"; Permissions: authusers-modify
Name: "{commonappdata}\Dynamologio\Logs"; Permissions: authusers-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Detect .NET Framework 4.7.2 or newer
function IsDotNet472Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
  begin
    // 461808 = .NET Framework 4.7.2 on Windows 10 April 2018 Update and all other OS versions
    Result := (Release >= 461808);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet472Installed() then
  begin
    MsgBox('Η εφαρμογή απαιτεί το Microsoft .NET Framework 4.7.2 ή νεότερο.' + #13#10 +
           'Παρακαλώ εγκαταστήστε το .NET Framework 4.7.2 offline installer πριν συνεχίσετε.', mbCriticalError, MB_OK);
    Result := False;
  end;
end;

[UninstallDelete]
; Clean temporary logs on uninstall, but PRESERVE user database and backups by default!
Type: files; Name: "{commonappdata}\Dynamologio\Logs\*.log"
