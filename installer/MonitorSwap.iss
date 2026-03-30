#define MyAppName "Monitor Swap"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\Release"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Setup]
AppId={{CFA0D1F1-9B7D-4E4A-B283-EC4E0F36487C}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher=MonitorSwap
AppComments=Rotate windows across selected monitors with global hotkeys.
DefaultDirName={localappdata}\Programs\MonitorSwap
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\MonitorSwap.exe
OutputDir={#OutputDir}
OutputBaseFilename=MonitorSwap-Setup-{#AppVersion}
SetupIconFile=..\Assets\MonitorSwap.ico
WizardImageFile=..\Assets\InstallerWizard.bmp
WizardSmallImageFile=..\Assets\InstallerWizardSmall.bmp
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=no
CloseApplications=yes
AppMutex=MonitorSwap.Singleton
VersionInfoVersion={#AppVersion}
VersionInfoCompany=MonitorSwap
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "autostart"; Description: "Start Monitor Swap when I sign in to Windows"; Flags: unchecked

[Files]
Source: "{#SourceDir}\MonitorSwap.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\MonitorSwap.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\MonitorSwap.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\MonitorSwap.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MonitorSwap"; ValueData: """{app}\MonitorSwap.exe"""; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\MonitorSwap"; ValueType: string; ValueName: "UiLanguage"; ValueData: "{code:GetAppLanguageCode}"

[Run]
Filename: "{app}\MonitorSwap.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function GetAppLanguageCode(Param: string): string;
begin
  if ActiveLanguage = 'korean' then
    Result := 'ko'
  else
    Result := 'en';
end;
