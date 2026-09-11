using System;
using System.IO;
using Microsoft.Win32;

namespace YouTubeLiveDesktop.Services
{
    /// <summary>
    /// Windows 시작 프로그램(레지스트리 Run 키) 등록/해제를 담당합니다.
    /// 별도의 설치 프로그램 옵션 없이도 앱 자체에서 자동 실행 여부를 켜고 끌 수 있습니다.
    /// </summary>
    public class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "YouTubeLiveDesktop";

        /// <summary>
        /// 게시(publish)된 self-contained exe로 실행 중인지 확인합니다.
        /// Visual Studio의 "디버그 시작"이나 dotnet run으로 실행하면 실제 프로세스가
        /// dotnet.exe이기 때문에, 이 상태로 등록하면 재부팅 후 dotnet.exe만 실행되고
        /// 곧바로 종료되는(깨진) 자동 시작 항목이 만들어집니다. 이를 방지합니다.
        /// </summary>
        public bool IsRunningFromPublishedExe()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return false;

            var fileName = Path.GetFileNameWithoutExtension(exePath);
            return !string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 자동 시작을 켜거나 끕니다. 등록/해제에 성공하면 true, 실패하면 false를 반환합니다.
        /// (예: 개발 모드로 실행 중이어서 켤 수 없는 경우, 레지스트리 접근이 거부된 경우 등)
        /// </summary>
        public bool SetEnabled(bool enabled)
        {
            try
            {
                // OpenSubKey가 아닌 CreateSubKey를 사용해, Run 키 자체가 없는 환경에서도
                // (일부 최소 설치본 등) 새로 만들어 등록할 수 있도록 합니다.
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                if (key == null) return false;

                if (enabled)
                {
                    if (!IsRunningFromPublishedExe())
                        return false;

                    var exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath)) return false;

                    key.SetValue(ValueName, $"\"{exePath}\"");
                }
                else
                {
                    if (key.GetValue(ValueName) != null)
                        key.DeleteValue(ValueName, throwOnMissingValue: false);
                }

                return true;
            }
            catch
            {
                // 레지스트리 접근 실패 등 예기치 못한 오류 시, 앱이 죽지 않고 실패로만 처리합니다.
                return false;
            }
        }

        /// <summary>현재 자동 시작이 등록되어 있는지 확인합니다.</summary>
        public bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }
    }
}
