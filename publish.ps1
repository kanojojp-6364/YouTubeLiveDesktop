# YouTube Live Desktop - Windows용 설치 프로그램(Setup.exe) 빌드 스크립트
# 사용법: 이 폴더(YouTubeLiveDesktop\)에서 PowerShell로 실행
#   .\publish.ps1
#
# 이 스크립트가 하는 일 (2단계):
#   1) dotnet publish로 자기 완결형(self-contained) 단일 실행파일을 만듭니다.
#      -> publish\YouTubeLiveDesktop.exe (별도 .NET 설치 불필요)
#   2) 컴퓨터에 Inno Setup이 설치되어 있으면, 그 exe를 감싸는 진짜 설치 프로그램
#      (installer_output\YouTubeLiveDesktopSetup.exe)을 자동으로 만듭니다.
#      이 Setup.exe를 실행해 "Install"을 누르면 추가 프로그램 설치 없이
#      바로 YouTube Live Desktop이 설치/실행됩니다.
#
# 참고: "YouTubeLiveDesktop.exe를 실행했더니 다른 프로그램을 설치하라고 나온다"는
# 문제는 대부분 publish\ 폴더가 아니라 bin\Debug 등 "프레임워크 종속" 빌드를
# 직접 실행했을 때 발생합니다(.NET 런타임이 없어서 설치를 유도하는 메시지).
# publish\ 폴더의 결과물(또는 아래에서 만들어지는 Setup.exe로 설치한 결과물)은
# .NET 런타임이 이미 포함되어 있어 그런 메시지가 뜨지 않습니다.

$ErrorActionPreference = "Stop"

Write-Host "[1/2] 자기 완결형 실행파일 빌드 중..." -ForegroundColor Cyan
dotnet publish YouTubeLiveDesktop.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish

Write-Host ""
Write-Host "빌드 완료: publish\YouTubeLiveDesktop.exe" -ForegroundColor Green
Write-Host "주의: 실행 PC에 'Microsoft Edge WebView2 Runtime'이 설치되어 있어야 합니다." -ForegroundColor Yellow
Write-Host "      Windows 10/11은 대부분 기본 내장되어 있으며, 없다면 아래에서 설치할 수 있습니다:" -ForegroundColor Yellow
Write-Host "      https://developer.microsoft.com/microsoft-edge/webview2/" -ForegroundColor Yellow
Write-Host ""

# Inno Setup Compiler(ISCC.exe)를 몇 가지 일반적인 설치 경로/환경변수 PATH에서 찾아봅니다.
$isccCandidates = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}

Write-Host "[2/2] 설치 프로그램(Setup.exe) 빌드..." -ForegroundColor Cyan
if ($iscc) {
    & $iscc "installer\YouTubeLiveDesktop.iss"
    Write-Host ""
    Write-Host "설치 프로그램 생성 완료: installer_output\YouTubeLiveDesktopSetup.exe" -ForegroundColor Green
    Write-Host "이 파일을 배포하세요. 실행 후 Install만 누르면 추가 설치 없이 바로 앱이 설치/실행됩니다." -ForegroundColor Green
}
else {
    Write-Host "Inno Setup이 설치되어 있지 않아 이 단계는 건너뜁니다." -ForegroundColor Yellow
    Write-Host "설치 프로그램(Setup.exe)까지 만들려면:" -ForegroundColor Yellow
    Write-Host "  1) https://jrsoftware.org/isinfo.php 에서 Inno Setup을 한 번만 설치" -ForegroundColor Yellow
    Write-Host "  2) 이 스크립트를 다시 실행하거나, 아래 명령을 직접 실행:" -ForegroundColor Yellow
    Write-Host "     iscc installer\YouTubeLiveDesktop.iss" -ForegroundColor Yellow
    Write-Host "  (지금은 publish\YouTubeLiveDesktop.exe 를 그대로 배포해도 정상 동작합니다." -ForegroundColor Yellow
    Write-Host "   단, publish 폴더의 파일을 그대로 실행해야 하며 bin\Debug 등에서 실행하면 안 됩니다.)" -ForegroundColor Yellow
}
