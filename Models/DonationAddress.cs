namespace YouTubeLiveDesktop.Models
{
    /// <summary>
    /// 후원(개발자 지원) 창에 표시할 코인 입금 주소 하나.
    /// XRP처럼 데스티네이션 태그가 필요한 코인은 Tag에 값을 넣습니다.
    /// </summary>
    public record DonationAddress(string CoinName, string Network, string Address, string? Tag = null);

    /// <summary>
    /// 실제 후원 주소 목록. 주소를 바꾸고 싶으면 이 파일만 수정하면 됩니다.
    /// </summary>
    public static class DonationAddresses
    {
        public static readonly DonationAddress[] All =
        {
            new("BTC", "Bitcoin", "3Md65gj4sZLb2rbXAdtkLKN9yMS4vKK8Lw"),
            new("ETH", "Ethereum", "0x0407839f4f78eb261c5c3c2eace72ccd94314cfe"),
            new("XRP", "XRP Ledger", "rYmTPYVPLv4EbhTN4Uw8nfvjyShLGESMV", "548580003"),
        };
    }

    /// <summary>
    /// 후원 창/트레이 아이콘 등에 표시할 개발자 정보. 바꾸고 싶으면 이 파일만 수정하면 됩니다.
    /// </summary>
    public static class DeveloperInfo
    {
        public const string Nickname = "hoya";
        public const string Email = "kanojojp@gmail.com";
    }
}
