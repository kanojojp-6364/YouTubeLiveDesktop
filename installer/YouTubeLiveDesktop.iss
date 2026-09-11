; YouTube Live Desktop - Inno Setup 스크립트
;
; 이 파일은 publish\YouTubeLiveDesktop.exe (자기 완결형 단일 실행파일)를
; 더블클릭 한 번으로 설치되는 "Setup.exe" 설치 프로그램으로 감싸줍니다.
;
; ※ 왜 필요한가?
;   YouTubeLiveDesktop.exe를 publish 폴더가 아닌 bin\Debug 등에서 직접 실행하면
;   해당 PC에 .NET 8 Desktop Runtime이 없을 경우 "다른 프로그램을 설치해야 합니다"
;   라는 안내가 뜹니다. 이는 publish\ 결과물이 아니라 "프레임워크 종속" 빌드를 실행했기
;   때문입니다. publish.ps1로 만든 publish\YouTubeLiveDesktop.exe는 .NET 런타임을
;   실행파일 안에 이미 포함한 "자기 완결형(self-contained)" 빌드라 별도 설치가 필요 없고,
;   이 설치 프로그램(Setup.exe)도 그 파일을 그대로 담아 배포합니다.
;   (Microsoft Edge WebView2 Runtime만 예외로, Windows 10/11 대부분에 기본 내장되어 있습니다.)
;
; 사용 방법:
;   1) 사용자 PC에 Inno Setup(무료, https://jrsoftware.org/isinfo.php)을 한 번만 설치합니다.
;   2) publish.ps1을 먼저 실행해 publish\YouTubeLiveDesktop.exe를 만듭니다.
;   3) 이 파일을 Inno Setup Compiler로 열고 Build > Compile을 누르거나,
;      명령줄에서 `iscc installer\YouTubeLiveDesktop.iss` 를 실행합니다.
;   4) installer_output\YouTubeLiveDesktopSetup-<버전>.exe (예: YouTubeLiveDesktopSetup-1.1.2.exe) 가 생성됩니다.
;      이 파일이 사용자에게 배포할 "설치 프로그램"입니다. 실행 후 Install만 누르면
;      추가 프로그램 설치 없이 바로 앱이 설치/실행됩니다.

; ※ 새 버전을 배포할 때: 아래 MyAppVersion을 YouTubeLiveDesktop.csproj의 <Version>과
;   똑같이 맞춰주세요(예: 1.1.0). 앱 안의 자동 업데이트 확인 기능은 csproj의 <Version>과
;   GitHub 릴리즈 태그(v1.1.0 등)를 비교하므로, 이 값 자체는 "제어판 > 프로그램" 등에
;   표시되는 용도지만 헷갈리지 않도록 항상 같이 올려주는 것이 좋습니다.
#define MyAppName "YouTube Live Desktop"
#define MyAppVersion "1.1.4"
#define MyAppPublisher "hoya"
#define MyAppExeName "YouTubeLiveDesktop.exe"
#define MyPublishDir "..\publish"
#define MyAppIcon "..\Resources\app.ico"

[Setup]
AppId={{7C8D9E10-4F2B-4C6A-9B1D-YTLIVEDESKTOP}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; 관리자 권한 없이 현재 사용자 폴더(LocalAppData)에 설치합니다.
; Program Files에 설치하면 매번 UAC(관리자 권한) 창이 뜨는데, 이 앱은
; 개인 바탕화면 꾸미기 용도이므로 그럴 필요가 없습니다.
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\installer_output
OutputBaseFilename=YouTubeLiveDesktopSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; 앱 자체의 자동 업데이트 기능이 새 설치 파일을 내려받아 실행하면, 실행 중이던
; 이전 버전은 그 전에 스스로 종료하지만(App.xaml.cs), 사용자가 이 Setup.exe를
; 수동으로 다시 실행하는 경우에도 대비해 앱이 켜져 있으면 자동으로 종료 후
; 설치하고 설치가 끝나면 다시 실행해 줍니다.
CloseApplications=yes
RestartApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
; 설치 프로그램(Setup.exe)/제거 프로그램 자체의 아이콘입니다. exe 파일 아이콘을 바꾸려면
; Resources\app.ico 를 교체한 뒤 dotnet publish부터 다시 해야 하고(csproj의 ApplicationIcon),
; 이 줄은 그 파일을 그대로 재사용해 Setup.exe도 같은 아이콘을 쓰게 해줍니다.
SetupIconFile={#MyAppIcon}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; publish\YouTubeLiveDesktop.exe 하나만 있으면 되는 자기 완결형 단일 파일이라
; 추가 DLL을 나열할 필요가 없습니다. (설치 전 publish.ps1 실행 필수)
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 설치 완료 후 바로 실행할지 물어봅니다. 여기서 실행되는 것은 방금 설치한
; 자기 완결형 exe이므로, 이 단계에서도 추가 설치 창은 뜨지 않습니다.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 프로그램 자체 설정(재생 URL, 볼륨, 재생 기록 등)은 %AppData%\YouTubeLiveDesktop 에 저장되며,
; 사용자가 다시 설치했을 때 설정을 잃지 않도록 제거 시 자동으로 지우지 않습니다.
Type: filesandordirs; Name: "{app}"

[Code]
// Windows 시작 시 자동 실행 등록/해제는 앱 자체의 "설정" 창 체크박스에서
// 처리합니다(설치 후 실행 파일 경로를 기준으로 스스로 등록하므로 더 안전합니다).
// 그래서 이 설치 프로그램은 레지스트리 Run 키를 직접 건드리지 않습니다.
