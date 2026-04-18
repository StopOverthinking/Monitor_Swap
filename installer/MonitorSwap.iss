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
CloseApplicationsFilter=MonitorSwap.exe
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

[CustomMessages]
english.RunningInstanceTitle=Close Monitor Swap
english.RunningInstancePrompt=Monitor Swap is currently running. Setup needs to close it before continuing.%n%nDo you want Setup to close the running app now?
english.RunningInstanceFailed=Setup could not close Monitor Swap automatically. Please close it manually and run Setup again.
korean.RunningInstanceTitle=Monitor Swap 종료
korean.RunningInstancePrompt=Monitor Swap가 현재 실행 중입니다. 설치를 계속하려면 먼저 종료해야 합니다.%n%n지금 실행 중인 앱을 설치기가 종료하도록 할까요?
korean.RunningInstanceFailed=설치기가 Monitor Swap를 자동으로 종료하지 못했습니다. 앱을 직접 종료한 뒤 설치기를 다시 실행해 주세요.

[Code]
function IsMonitorSwapRunning: Boolean;
begin
  Result := CheckForMutexes('MonitorSwap.Singleton');
end;

function TryCloseMonitorSwap: Boolean;
var
  ResultCode: Integer;
  Attempts: Integer;
begin
  Result := True;

  if not IsMonitorSwapRunning then
    Exit;

  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM MonitorSwap.exe /T',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  for Attempts := 1 to 12 do
  begin
    if not IsMonitorSwapRunning then
      Exit;

     Sleep(250);
  end;

  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM MonitorSwap.exe /T',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  for Attempts := 1 to 20 do
  begin
    if not IsMonitorSwapRunning then
      Exit;

    Sleep(250);
  end;

  Result := not IsMonitorSwapRunning;
end;

function InitializeSetup: Boolean;
begin
  Result := True;

  if not IsMonitorSwapRunning then
    Exit;

  if MsgBox(
       ExpandConstant('{cm:RunningInstancePrompt}'),
       mbConfirmation,
       MB_YESNO) <> IDYES then
  begin
    Result := False;
    Exit;
  end;

  if not TryCloseMonitorSwap then
  begin
    MsgBox(
      ExpandConstant('{cm:RunningInstanceFailed}'),
      mbError,
      MB_OK);
    Result := False;
  end;
end;

function GetAppLanguageCode(Param: string): string;
begin
  if ActiveLanguage = 'korean' then
    Result := 'ko'
  else
    Result := 'en';
end;
