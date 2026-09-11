using System;
using System.IO;
using System.Text.Json;
using YouTubeLiveDesktop.Models;

namespace YouTubeLiveDesktop.Services
{
    /// <summary>
    /// 설정 파일의 로드/저장을 담당합니다 (Storage Layer).
    /// 쿠키/로그인 정보는 저장하지 않고, URL과 옵션 값만 로컬 AppData에 저장합니다.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YouTubeLiveDesktop");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    // 처음 실행(설정 파일이 아직 없음)일 때만 기본 재생 기록 2개를 넣어줍니다.
                    // 이후 사용자가 전체 삭제를 해도 여기서 다시 채워 넣지 않습니다.
                    var fresh = new AppSettings();
                    SeedDefaultHistory(fresh);
                    return fresh;
                }

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                return settings ?? new AppSettings();
            }
            catch
            {
                // 설정 파일이 손상된 경우에도 프로그램이 죽지 않도록 기본값으로 복구합니다.
                return new AppSettings();
            }
        }

        private static void SeedDefaultHistory(AppSettings settings)
        {
            settings.History.Add(new HistoryEntry
            {
                RawInput = "https://www.youtube.com/embed/jmVmZlsQIL8",
                DisplayName = "YouTube jmVmZlsQIL8",
                Kind = nameof(SourceKind.YouTube),
                LastUsedUtc = DateTime.UtcNow
            });
            settings.History.Add(new HistoryEntry
            {
                RawInput = "https://www.youtube.com/embed/LGuktGeHQxI",
                DisplayName = "YouTube LGuktGeHQxI",
                Kind = nameof(SourceKind.YouTube),
                LastUsedUtc = DateTime.UtcNow
            });
        }

        public void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // 저장 실패는 치명적이지 않으므로 무시합니다 (다음 변경 시 재시도됩니다).
            }
        }
    }
}
