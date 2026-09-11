# YouTube Live Desktop

YouTube Live 방송을 Windows 바탕화면의 배경 레이어에서 재생하는 프로그램입니다.
(기획서 `YouTube_Live_Desktop_개발기획서.pdf`의 1차 목표: 단일 모니터·단일 URL 안정 재생 기준으로 구현)

## 핵심 동작

- C# / .NET 8 (WPF) + Microsoft Edge WebView2
- Progman/WorkerW 구조를 이용해 재생 창을 바탕화면 아이콘 뒤 배경 레이어에 삽입
- **YouTube뿐 아니라 Vimeo URL, 컴퓨터에 저장된 로컬 동영상 파일도 재생 가능**
  (YouTube는 공식 IFrame Player API, Vimeo는 공식 Player SDK 사용 — 화면 스크래핑 방식 아님.
  로컬 파일은 WebView2 가상 호스트 매핑으로 서빙되는 HTML5 `<video>`로 재생)
- 16:9 비율을 유지한 채 레터박스/필러박스로 화면에 맞춰 표시
- 시스템 트레이 아이콘에서 재생/일시정지, 음소거, 볼륨(사전 설정값 25%/50%/75%/100% + ±10% 버튼),
  설정 열기, 개발자 도구(F12, 문제 진단용), 배경 비활성화, 종료 제어
  - 이전 버전의 트레이 메뉴 내 드래그형 볼륨 슬라이더는 Windows 팝업 메뉴와 마우스 캡처가
    충돌해 잘 동작하지 않는 경우가 있어(잘 알려진 WinForms 제약), 클릭 한 번으로 확정되는
    메뉴 항목 방식으로 교체했습니다. 세밀한 조절은 설정 창의 슬라이더를 사용하세요.
- 음소거/볼륨은 트레이든 설정 창이든 어디서 바꿔도 즉시 재생에 반영되고 서로 동기화됩니다.
  또한 재생 페이지가 아직 준비되기 전에 들어온 명령은 버리지 않고 큐에 쌓아뒀다가 준비되는
  즉시 전송합니다(앱을 막 켰을 때 음소거/볼륨 조작이 무반응처럼 보이던 문제의 원인이었습니다).
- **재생 기록**: 재생에 성공했던 URL/파일을 최신순 최대 20개까지 설정 창에 남기고,
  더블클릭하면 다시 엽니다. 체크 후 선택 삭제, 전체 삭제도 가능합니다.
- **개발자 후원하기**: 트레이 메뉴에서 열 수 있는 후원 창(BTC/ETH/XRP). 주소는
  `Models\DonationAddress.cs`, 개발자 닉네임/이메일은 `Models\DonationAddress.cs`의
  `DeveloperInfo`에 있으며, QR코드는 외부 서비스 없이 앱 안에서 직접 생성합니다
  (QRCoder 라이브러리, 네트워크 통신 없음).
- **자동 업데이트 확인**: 시작 15초 후 및 이후 6시간마다 GitHub Releases에서 새 버전이
  있는지 확인해, 있으면 트레이 메뉴 맨 위에 굵은 글씨로 "새 버전 사용 가능"이 나타납니다.
  클릭하면 설치 파일을 자동으로 내려받은 뒤 설치 프로그램을 실행하고 현재 앱은 종료됩니다
  (설치 마법사에서 Install만 누르면 끝). **배포 전 `Services\UpdateService.cs` 상단의
  `GitHubOwner`/`GitHubRepo`를 실제 GitHub 계정/저장소 이름으로 반드시 바꿔야 동작합니다**
  (자세한 배포 절차는 그 파일 주석 참고). 아직 설정하지 않았거나 인터넷/GitHub 응답이
  없으면 조용히 아무 일도 일어나지 않습니다(부가 기능이라 실패해도 앱 사용에는 지장 없음).
- 설정(URL, 볼륨, 음소거, 자동실행, 날씨 지역, 재생 기록)은 `%AppData%\YouTubeLiveDesktop\settings.json`에 저장
- Windows 시작 프로그램 등록/해제를 앱 내에서 직접 처리 (레지스트리 Run 키, 별도 설치 프로그램 불필요)
  - 게시된(publish) exe로 실행 중일 때만 등록할 수 있으며, `dotnet run`/Visual Studio 디버그
    실행처럼 개발 모드일 때는 등록에 실패했다는 안내와 함께 자동으로 체크가 해제됩니다
    (그렇지 않으면 재부팅 후 dotnet.exe만 실행되고 바로 종료되는 깨진 자동 실행 항목이 생깁니다).
- 오류 종류에 따라 제한된 횟수만 자동 재로드 (재시도해도 의미 없는 오류는 화면 안내만 표시, 무한 깜빡임 방지)
- **화면 정중앙 오버레이**: 오늘 날짜/요일/시간(1초마다 갱신)과 설정한 지역의 현재 날씨(기온 + 상태 이모지)를 표시
  - 날씨는 [Open-Meteo](https://open-meteo.com) 무료 공개 API(키 불필요)로 조회, 15분마다 자동 갱신
  - 지역은 자유 입력이 아니라 **드롭다운 목록에서만 선택**합니다(`Models/WeatherRegion.cs`에 좌표가 고정되어 있어
    지역명 오타·표기 불일치로 날씨가 안 뜨는 문제가 생기지 않습니다). 선택 후 적용하면 영상 재로드 없이 바로 반영됩니다.
  - 목록에 없는 지역이 필요하면 `Models/WeatherRegion.cs`의 `WeatherRegions.All`에 이름과 위도/경도를 한 줄 추가하면 됩니다.
  - 아이콘은 별도 이미지 등록 없이 기상 상태(맑음/흐림/비/눈/뇌우 등)에 맞는 이모지가 자동 표시

## 프로젝트 구조

```
YouTubeLiveDesktop/
├─ Models/
│  ├─ AppSettings.cs
│  ├─ HistoryEntry.cs             # 재생 기록 한 항목
│  ├─ PlaybackSource.cs           # 입력값을 해석한 결과(YouTube/Vimeo/Local)
│  └─ WeatherRegion.cs            # 날씨 지역 드롭다운 목록(좌표 고정)
├─ Services/
│  ├─ WebViewService.cs           # WebView2 초기화, 소스 해석(YouTube/Vimeo/로컬), 재생 제어, 명령 큐잉
│  ├─ DesktopWallpaperService.cs  # Progman/WorkerW 탐색 및 창 삽입
│  ├─ SettingsService.cs          # 설정 로드/저장
│  ├─ StartupService.cs           # Windows 시작 프로그램 등록/해제 (게시된 exe 여부 자체 점검)
│  └─ TrayService.cs              # 시스템 트레이 아이콘/메뉴 (볼륨은 클릭형 메뉴)
├─ Views/
│  ├─ MainWindow.xaml(.cs)        # 설정 화면 (URL/파일 입력, 볼륨, 음소거, 자동실행, 날씨, 재생 기록)
│  └─ WallpaperWindow.xaml(.cs)   # 바탕화면에 삽입되는 실제 재생 창
├─ Resources/
│  └─ wallpaper.html              # 16:9 유지 + YouTube/Vimeo/로컬 video 공통 재생 래퍼 + 날씨/시계 오버레이
├─ App.xaml(.cs)                  # 조립/부트스트랩 (Application Layer)
├─ appsettings.json               # 기본값 참고 템플릿 (실사용 설정은 AppData에 저장)
└─ YouTubeLiveDesktop.csproj
```

> 참고: 기획서의 권장 구조에는 `Program.cs`가 있었지만, WPF는 `App.xaml`이 진입점을
> 자동 생성하는 표준 부트스트랩 방식이 더 안정적이라 판단해 `App.xaml.cs`에 모든
> 초기화 로직을 두었습니다. 필요하면 언제든 별도 `Program.cs`로 분리할 수 있습니다.

## 빌드 방법

### 방법 A. Visual Studio 2022
1. `YouTubeLiveDesktop.csproj`를 Visual Studio 2022(이상)에서 엽니다.
2. NuGet 패키지 복원(자동) 후 `Release / x64`로 빌드하거나 `F5`로 실행합니다.

### 방법 B. 명령줄 (.NET 8 SDK 설치 필요)
```powershell
dotnet restore
dotnet build -c Release
```

### 배포용 설치 프로그램(Setup.exe) 만들기
```powershell
.\publish.ps1
```
이 한 번의 명령으로 두 단계가 실행됩니다.

1. `dotnet publish`로 **자기 완결형(self-contained) 단일 EXE**를 만듭니다
   (`publish\YouTubeLiveDesktop.exe`). .NET 런타임이 이 파일 안에 이미 포함되어 있어
   대상 PC에 별도로 .NET을 설치할 필요가 없습니다.
2. PC에 **Inno Setup**(무료, <https://jrsoftware.org/isinfo.php>)이 설치되어 있으면,
   위 EXE를 감싸는 진짜 설치 프로그램을 자동으로 만듭니다:
   `installer_output\YouTubeLiveDesktopSetup-<버전>.exe` (예: `YouTubeLiveDesktopSetup-1.1.2.exe`)

이 파일을 배포하면 사용자는 이 파일 하나만 실행해 **Install**을
누르는 것으로 설치가 끝나고, 추가로 다른 프로그램을 설치하라는 창은 뜨지 않습니다
(관리자 권한도 필요 없도록 사용자 폴더(`%LocalAppData%\Programs`)에 설치합니다).
설치 후에는 시작 메뉴/바탕화면 바로가기로 실행하며, Windows 자동 시작은 앱을 한 번
실행한 뒤 설정 창의 "Windows 시작 시 자동 실행" 체크박스로 켤 수 있습니다.

Inno Setup을 아직 설치하지 않았다면 2단계는 건너뛰고 1단계 결과(`publish\YouTubeLiveDesktop.exe`)만
만들어집니다 — 이 파일 자체도 정상 동작하는 완전한 프로그램이니 그대로 배포해도 됩니다.
다만 **`publish\` 폴더 안의 파일을 그대로 실행**해야 하며, `bin\Debug\...` 폴더의 exe를
직접 실행하면 (.NET 런타임이 빠진 "프레임워크 종속" 빌드라) "다른 프로그램을 설치해야 합니다"
라는 안내가 뜰 수 있습니다.

> Inno Setup 스크립트는 `installer\YouTubeLiveDesktop.iss`에 있습니다. 설치 폴더, 아이콘,
> 버전 등을 바꾸고 싶으면 이 파일을 직접 수정한 뒤 다시 `.\publish.ps1`을 실행하면 됩니다.

## 무료 배포 및 자동 업데이트 설정 (최초 1회)

앱을 다른 사람에게 무료로 배포하고, 이후 새 버전을 자동으로 인식하게 하려면 GitHub
Releases를 사용합니다(무료). **처음 한 번만** 설정하면 됩니다.

1. GitHub 계정이 없으면 <https://github.com>에서 무료로 만듭니다.
2. 새 저장소(Repository)를 만듭니다 — 이름은 자유입니다(예: `YouTubeLiveDesktop`).
   소스코드를 공개하고 싶지 않다면 **Private**로 만들어도 배포/업데이트 기능은
   그대로 동작합니다(Release로 올린 설치 파일은 Private 저장소라도 그 저장소의
   Releases 페이지 링크를 통해 누구나 내려받을 수 있습니다. 완전히 비공개로
   막고 싶다면 저장소를 Public으로 유지하는 것이 가장 간단합니다).
3. `Services\UpdateService.cs` 파일을 열어 맨 위쪽의 두 줄을 실제 값으로 바꿉니다:
   ```csharp
   private const string GitHubOwner = "YOUR_GITHUB_USERNAME"; // 예: "hoya"
   private const string GitHubRepo = "YouTubeLiveDesktop";     // 실제 저장소 이름
   ```
4. `.\publish.ps1`로 `installer_output\YouTubeLiveDesktopSetup-<버전>.exe`를 다시 만듭니다.
5. 이 Setup.exe 파일을 사용자들에게 배포합니다(다운로드 링크로 공유, 웹사이트 게시 등 자유).

### 이후 새 버전을 낼 때마다

1. `YouTubeLiveDesktop.csproj`의 `<Version>`을 올립니다 (예: `1.0.0` → `1.1.0`).
2. `.\publish.ps1`을 실행해 새 `YouTubeLiveDesktopSetup-<버전>.exe`를 만듭니다.
3. GitHub 저장소 페이지 → **Releases** → **Draft a new release**를 클릭합니다.
4. 태그(Tag)에 `v1.1.0`처럼 **버전 형식**으로 입력합니다(맨 앞 `v`는 있어도 없어도 됩니다).
5. 방금 만든 `YouTubeLiveDesktopSetup-<버전>.exe`를 화면에 끌어다 놓아 첨부(Assets)한 뒤
   **Publish release**를 누릅니다.

이렇게 올려두면, 이미 설치되어 실행 중인 모든 사용자의 앱이 실행 후 15초 이내(및 이후
6시간마다)에 새 버전을 자동으로 인식해 트레이 메뉴 맨 위에 굵은 글씨로
"⬆ 새 버전 사용 가능 (v1.1.0) - 클릭해서 설치"가 나타납니다. 사용자가 클릭하면
Setup.exe를 자동으로 내려받아 실행하고(설치 마법사에서 Install만 누르면 끝), 기존
앱은 파일이 덮어써질 수 있도록 스스로 종료됩니다. `GitHubOwner`를 아직 설정하지
않았거나 인터넷/GitHub 응답이 없으면 이 기능은 조용히 아무 일도 하지 않습니다
(부가 기능이므로 실패해도 앱 사용 자체에는 지장이 없습니다).

## 사용법

1. 설치 프로그램(`YouTubeLiveDesktopSetup-<버전>.exe`)을 실행해 **Install**을 누릅니다.
   (설치 프로그램 없이 `publish\YouTubeLiveDesktop.exe`를 바로 실행해도 됩니다.)
2. 처음 실행 시 뜨는 설정 창에 재생할 URL/파일을 입력합니다.
   - YouTube: `https://www.youtube.com/watch?v=XXXXXXXXXXX`, `https://www.youtube.com/@채널명/live`,
     `https://youtu.be/XXXXXXXXXXX`
   - Vimeo: `https://vimeo.com/XXXXXXXXX`
   - 로컬 동영상 파일: **찾아보기...** 버튼으로 mp4/mkv/avi/mov/webm/wmv 파일을 직접 선택하거나
     파일 경로를 그대로 입력란에 붙여넣으면 됩니다.
3. **적용**을 누르면 바로 재생되고, **저장**을 누르면 다음 실행 시에도 유지됩니다.
   재생에 성공하면 자동으로 아래 **재생 기록** 목록에 추가됩니다(더블클릭으로 재열기).
4. 설정 창을 닫아도 프로그램은 트레이 아이콘에 남아 계속 실행됩니다.
5. 트레이 아이콘(작업표시줄 우측 하단)을 우클릭하면 재생/일시정지, 음소거, 볼륨,
   설정 열기, **개발자 후원하기**, 개발자 도구, 배경 비활성화, 종료 메뉴를 사용할 수 있습니다.
   새 버전이 나오면 메뉴 맨 위에 굵은 글씨로 업데이트 항목이 자동으로 나타납니다.
6. 완전히 끄려면 트레이 메뉴의 **종료**를 사용하세요. 종료하면 바탕화면은 원래
   상태(정적 배경화면)로 즉시 복구됩니다.

## 알려진 제한사항 / 향후 확장

기획서 기준 1차 목표(단일 모니터·단일 URL)에 집중해 구현했습니다. 아래는 향후 확장 항목입니다.

- 다중 모니터별 개별 URL 재생, 여러 URL 순환 재생, 화면 분할 — 미구현 (구조상 확장 가능하도록 서비스 분리)
- 전체화면 앱 실행 감지 시 자동 일시정지 — 미구현
- 설치형 EXE 패키징 — `installer\YouTubeLiveDesktop.iss`(Inno Setup)로 구현했습니다
  (`.\publish.ps1` 실행 시 Inno Setup이 설치되어 있으면 자동으로
  `installer_output\YouTubeLiveDesktopSetup-<버전>.exe`가 만들어집니다). MSI가 꼭 필요하면
  WiX Toolset으로 별도 제작 가능합니다.
- YouTube는 페이지 구조/정책을 자주 바꾸므로, `/@채널/live` 같은 리다이렉트 기반 URL의
  videoId 해석 로직(`WebViewService.ResolveYouTubeVideoIdAsync`)은 실제 환경에서 검증이 필요합니다.
- WorkerW 탐색 로직(`DesktopWallpaperService`)은 Windows 버전/Explorer 빌드에 따라
  동작이 달라질 수 있어 대상 PC에서 반드시 실제 테스트가 필요합니다.

## 보안/개인정보

- 사용자가 입력한 URL 외의 임의 사이트로는 이동하지 않습니다.
- 로그인/쿠키 정보를 자체 서버로 전송하지 않으며, WebView2 프로필은
  `%AppData%\YouTubeLiveDesktop\WebView2Data`에 로컬로만 저장됩니다.
