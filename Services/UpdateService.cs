using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace YouTubeLiveDesktop.Services
{
    /// <summary>새로 나온 버전 정보(GitHub Releases에서 읽어온 결과).</summary>
    public record UpdateInfo(string Version, string DownloadUrl, string ReleaseUrl);

    /// <summary>
    /// GitHub Releases를 통한 자동 업데이트 확인/다운로드를 담당합니다.
    ///
    /// ★★★ 배포 전 꼭 설정하세요 ★★★
    /// 아래 GitHubOwner / GitHubRepo 를 실제로 배포에 사용할 GitHub 계정명/저장소 이름으로
    /// 바꾸세요 (예: 계정이 kanojojp이고 저장소 이름이 YouTubeLiveDesktop이면
    /// GitHubOwner = "kanojojp", GitHubRepo = "YouTubeLiveDesktop").
    ///
    /// 새 버전을 배포하는 절차:
    ///   1) YouTubeLiveDesktop.csproj의 &lt;Version&gt;을 올립니다 (예: 1.0.0 -> 1.1.0).
    ///   2) .\publish.ps1 로 installer_output\YouTubeLiveDesktopSetup.exe를 새로 만듭니다.
    ///   3) GitHub 저장소의 "Releases" 탭에서 "Draft a new release"로 새 릴리즈를 만들고,
    ///      태그 이름을 "v1.1.0" 처럼 버전 형식으로 입력한 뒤, 위 Setup.exe 파일을
    ///      Assets로 첨부해서 게시(Publish)합니다.
    /// 이렇게만 해두면 실행 중인 모든 앱이 자동으로 새 버전을 인식해 트레이에 표시합니다.
    ///
    /// 저장소가 아직 없거나 GitHubOwner를 설정하지 않은 경우, 이 서비스는 확인에 실패한 것으로
    /// 처리해 조용히 넘어갑니다(부가 기능이므로 실패해도 앱 사용에는 지장이 없습니다).
    /// </summary>
    public class UpdateService
    {
        private const string GitHubOwner = "YOUR_GITHUB_USERNAME"; // TODO: 실제 GitHub 계정명으로 변경
        private const string GitHubRepo = "YouTubeLiveDesktop";     // TODO: 실제 저장소 이름으로 변경

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // GitHub API는 User-Agent 헤더가 없으면 요청을 거부합니다.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YouTubeLiveDesktop-UpdateChecker");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        /// <summary>현재 실행 중인 버전보다 새로운 릴리즈가 있으면 정보를 반환하고, 없거나 확인에 실패하면 null을 반환합니다.</summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            if (GitHubOwner == "YOUR_GITHUB_USERNAME") return null; // 아직 저장소 정보가 설정되지 않음

            try
            {
                var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                var json = await Http.GetStringAsync(url);
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                if (release == null || string.IsNullOrWhiteSpace(release.TagName)) return null;

                var tag = release.TagName.TrimStart('v', 'V');
                if (!Version.TryParse(tag, out var latestVersion)) return null;

                var current = GetCurrentVersion();
                if (latestVersion <= current) return null;

                var asset = release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset == null) return null;

                return new UpdateInfo(tag, asset.BrowserDownloadUrl, release.HtmlUrl);
            }
            catch
            {
                // 저장소가 없거나(404), 인터넷 연결이 없거나, GitHub 응답 오류 등 어떤 이유로든
                // 확인에 실패하면 조용히 넘어갑니다.
                return null;
            }
        }

        private static Version GetCurrentVersion()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v ?? new Version(0, 0, 0, 0);
        }

        /// <summary>설치 파일을 임시 폴더로 다운로드하고, 저장된 경로를 반환합니다.</summary>
        public async Task<string> DownloadInstallerAsync(string downloadUrl)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"YouTubeLiveDesktopSetup_{Guid.NewGuid():N}.exe");

            await using var responseStream = await Http.GetStreamAsync(downloadUrl);
            await using var fileStream = File.Create(tempPath);
            await responseStream.CopyToAsync(fileStream);

            return tempPath;
        }

        private class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;

            [JsonPropertyName("assets")]
            public List<GitHubAsset> Assets { get; set; } = new();
        }

        private class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}
