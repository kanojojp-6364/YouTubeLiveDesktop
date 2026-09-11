using System;
using System.Text.Json.Serialization;

namespace YouTubeLiveDesktop.Models
{
    /// <summary>
    /// 실제로 재생을 성공시킨 URL/파일 경로 기록 하나. 설정 창의 "최근 재생 목록"에 표시되어
    /// 다시 클릭 한 번으로 열어볼 수 있게 합니다.
    /// </summary>
    public class HistoryEntry
    {
        /// <summary>사용자가 입력했던 원본 URL 또는 로컬 파일 경로 (다시 열 때 그대로 재사용).</summary>
        public string RawInput { get; set; } = string.Empty;

        /// <summary>목록에 보여줄 이름 (영상 ID/파일명 등).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>"YouTube" / "Vimeo" / "Local" (SourceKind 문자열).</summary>
        public string Kind { get; set; } = string.Empty;

        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 설정 창 목록에서 삭제 대상으로 체크했는지 여부. 화면 표시용 상태일 뿐이라
        /// settings.json에는 저장하지 않습니다.
        /// </summary>
        [JsonIgnore]
        public bool IsChecked { get; set; }

        public override string ToString()
        {
            var kindLabel = Kind switch
            {
                nameof(SourceKind.YouTube) => "YouTube",
                nameof(SourceKind.Vimeo) => "Vimeo",
                nameof(SourceKind.Local) => "로컬 파일",
                _ => Kind
            };
            return $"[{kindLabel}] {DisplayName}";
        }
    }
}
