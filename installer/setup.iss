; =====================================================================
; Inno Setup 6.x Script for ΔΥΝΑΜΟΛΟΓΙΟ (Offline Workstation Installer)
; Target Platforms: Windows 7 SP1 (x86/x64), Windows 10 (x64), Windows 11 (x64)
; =====================================================================

#define MyAppName "ΔΥΝΑΜΟΛΟΓΙΟ"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Dynamologio Project"
#define MyAppExeName "Dynamologio.exe"

[Setup]
AppId={{D37B40A5-5028-48D7-9092-23097C224B2C}
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
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1sp1
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "greek"; MessagesFile: "compiler:Languages\Greek.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Release Binaries (Air-gapped deployment, zero network dependencies)
Source: "..\src\Dynamologio.App\bin\Release\net472\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\templates-reference\Standard_Dynamologio_Template.xlsx"; DestDir: "{commonappdata}\Dynamologio\Templates"; Flags: ignoreversion uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Detect .NET Framework 4.7.2 or higher
function IsDotNet472Installed(): Boolean;
var
  release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) then
  begin
    // 461808 = .NET Framework 4.7.2 on Windows 10 April 2018 Update and earlier
    // 461814 = .NET Framework 4.7.2 on other Windows OS versions
    Result := (release >= 461808);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet472Installed() then
  begin
    MsgBox('Η εφαρμογή απαιτεί την εγκατάσταση του Microsoft .NET Framework 4.7.2 ή νεότερου.' + #13#10 +
           'Παρακαλώ εγκαταστήστε το .NET Framework 4.7.2 και εκτελέστε ξανά την εγκατάσταση.', mbCriticalError, MB_OK);
    Result := False;
  end;
end;
