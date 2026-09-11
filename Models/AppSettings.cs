using System.Collections.Generic;

namespace YouTubeLiveDesktop.Models
{
    /// <summary>
    /// 사용자가 저장한 설정을 표현하는 모델.
    /// %AppData%\YouTubeLiveDesktop\settings.json 에 직렬화되어 저장됩니다 (Storage Layer).
    /// </summary>
    public class AppSettings
    {
        /// <summary>재생할 YouTube Live(또는 일반 영상) URL.</summary>
        public string LiveUrl { get; set; } = string.Empty;

        /// <summary>마지막으로 재생에 성공한 videoId (재시작 시 재해석 없이 빠르게 복원하기 위한 캐시).</summary>
        public string? LastResolvedVideoId { get; set; }

        /// <summary>0~100 범위의 볼륨.</summary>
        public int Volume { get; set; } = 50;

        /// <summary>음소거 여부. 브라우저 자동재생 정책을 고려해 기본값은 true(음소거) 입니다.</summary>
        public bool Muted { get; set; } = true;

        /// <summary>Windows 시작 시 자동 실행 여부.</summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>바탕화면 배경 재생 활성화 여부 (트레이 메뉴에서 끄고 켤 수 있음).</summary>
        public bool WallpaperEnabled { get; set; } = true;

        /// <summary>재생 / 일시정지 상태.</summary>
        public bool IsPlaying { get; set; } = true;

        /// <summary>화면 중앙에 표시할 날씨 조회 지역(도시명). 비어 있으면 날씨는 표시하지 않습니다.</summary>
        public string WeatherRegion { get; set; } = string.Empty;

        /// <summary>재생에 성공했던 URL/파일 기록 (최신순, 최대 20개). 설정 창에서 다시 열 수 있습니다.</summary>
        public List<HistoryEntry> History { get; set; } = new();
    }
}
