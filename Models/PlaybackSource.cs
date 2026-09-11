namespace YouTubeLiveDesktop.Models
{
    /// <summary>재생 소스의 종류.</summary>
    public enum SourceKind
    {
        YouTube,
        Vimeo,
        Local
    }

    /// <summary>
    /// 사용자가 입력한 URL/파일 경로를 해석한 결과입니다.
    /// Value는 종류에 따라 의미가 다릅니다: YouTube=videoId, Vimeo=숫자 영상 ID,
    /// Local=가상 호스트로 매핑되어 브라우저가 바로 열 수 있는 https 주소.
    /// </summary>
    public record PlaybackSource(SourceKind Kind, string Value, string DisplayName);
}
