#define AppVersion "1.0.1"
#ifdef InstallSmoke
  #define InstallId "{{DC29BD00-83DA-43E5-9AEC-C7C51837AB16}"
  #define RunValueName "ASnapdo-InstallSmoke"
#else
  #define InstallId "{{B1BF7543-3849-4CCB-B30D-9FB40127BC20}"
  #define RunValueName "ASnapdo"
#endif
[Setup]
AppId={#InstallId}
AppName=ASnapdo
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
AppPublisher=AAStudio.kr
LicenseFile=..\LICENSE.txt
DefaultDirName={commonpf}\ASnapdo
UsePreviousAppDir=no
DefaultGroupName=ASnapdo
#ifdef InstallSmoke
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\release\setup
OutputBaseFilename=asnapdo_inst_x64_{#AppVersion}
SetupIconFile=..\src\ASnapdo\Assets\ASnapdo.ico
UninstallDisplayIcon={app}\ASnapdo.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\ASnapdo.NativeTray,Local\ASnapdo.Editor
CloseApplications=no
RestartApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로가기 만들기"; Flags: unchecked
Name: "startup"; Description: "Windows 로그인 시 자동 실행 (트레이 대기)"

[Messages]
korean.WizardLicense=무료 이용약관
korean.LicenseLabel=개인 · 기업 · 관공서 모두 무료로 사용할 수 있습니다.
korean.LicenseLabel3=제작사: AAStudio.kr. 아래 무료 이용약관을 확인한 뒤 동의해 주세요.
english.WizardLicense=Free Software License
english.LicenseLabel=Free for individuals, businesses and public institutions.
english.LicenseLabel3=Publisher: AAStudio.kr. Please read and accept the free software terms below.

[Files]
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\app\ASnapdo.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\app\editor\*"; DestDir: "{app}\editor"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ASnapdo"; Filename: "{app}\ASnapdo.exe"
Name: "{autodesktop}\ASnapdo"; Filename: "{app}\ASnapdo.exe"; Tasks: desktopicon

[Run]
#ifdef InstallSmoke
Filename: "{app}\ASnapdo.exe"; Parameters: "--enable-startup-test"; Tasks: startup; Flags: runasoriginaluser runhidden waituntilterminated
#else
Filename: "{app}\ASnapdo.exe"; Parameters: "--enable-startup"; Tasks: startup; Flags: runasoriginaluser runhidden waituntilterminated
#endif
Filename: "{app}\ASnapdo.exe"; Parameters: "--background"; Flags: nowait runasoriginaluser skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
end;
