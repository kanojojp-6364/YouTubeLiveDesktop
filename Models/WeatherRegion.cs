namespace YouTubeLiveDesktop.Models
{
    /// <summary>
    /// 날씨 표시용 지역 하나(이름 + 좌표)를 나타냅니다.
    /// 좌표를 미리 고정해두면 사용자가 지역명을 잘못 입력해 날씨 조회가 실패하는 문제가 생기지 않습니다.
    /// </summary>
    public record WeatherRegion(string Name, double Latitude, double Longitude);

    /// <summary>
    /// 설정 화면 드롭다운에 표시되는 선택 가능한 지역 목록입니다 (주요 시/도 기준).
    /// 자유 입력 대신 이 목록에서만 고르게 해 오타·지역명 불일치로 날씨가 안 뜨는 문제를 원천 차단합니다.
    /// </summary>
    public static class WeatherRegions
    {
        public static readonly WeatherRegion[] All =
        {
            new("서울", 37.5665, 126.9780),
            new("부산", 35.1796, 129.0756),
            new("대구", 35.8714, 128.6014),
            new("인천", 37.4563, 126.7052),
            new("광주", 35.1595, 126.8526),
            new("대전", 36.3504, 127.3845),
            new("울산", 35.5384, 129.3114),
            new("세종", 36.4801, 127.2890),
            new("수원", 37.2636, 127.0286),
            new("성남", 37.4201, 127.1262),
            new("고양", 37.6584, 126.8320),
            new("용인", 37.2411, 127.1776),
            new("청주", 36.6424, 127.4890),
            new("천안", 36.8151, 127.1139),
            new("전주", 35.8242, 127.1480),
            new("포항", 36.0190, 129.3435),
            new("창원", 35.2281, 128.6811),
            new("제주", 33.4996, 126.5312),
            new("춘천", 37.8813, 127.7298),
            new("강릉", 37.7519, 128.8761),
            new("여수", 34.7604, 127.6622),
            new("목포", 34.8118, 126.3922),
            new("경주", 35.8562, 129.2247),
            new("안동", 36.5684, 128.7294),
            new("진주", 35.1800, 128.1076),
        };

        public static WeatherRegion? Find(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            foreach (var region in All)
            {
                if (region.Name == name) return region;
            }
            return null;
        }
    }
}
