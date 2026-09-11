using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using YouTubeLiveDesktop.Models;

namespace YouTubeLiveDesktop.Services
{
    /// <summary>
    /// WebView2 초기화, 소스(YouTube/Vimeo/로컬 파일) 로딩, 재생 제어, 오류 처리/복구를 담당합니다 (WebView Layer).
    /// YouTube는 공식 IFrame Player API, Vimeo는 공식 Player SDK를 사용합니다(화면 스크래핑 아님).
    /// 로컬 파일은 WebView2 가상 호스트 매핑으로 해당 폴더를 서빙해 일반 HTML5 &lt;video&gt;로 재생합니다.
    /// </summary>
    public class WebViewService
    {
        // watch?v=, youtu.be/, /live/, /shorts/, /embed/ 뒤에 오는 11자리 videoId를 추출합니다.
        private static readonly Regex YouTubeIdRegex = new(
            @"(?:v=|\/live\/|youtu\.be\/|\/shorts\/|\/embed\/)([a-zA-Z0-9_-]{11})",
            RegexOptions.Compiled);

        // vimeo.com/12345678, vimeo.com/channels/xxx/12345678, player.vimeo.com/video/12345678 등을 지원합니다.
        private static readonly Regex VimeoIdRegex = new(
            @"vimeo\.com/(?:video/|channels/[^/]+/|groups/[^/]+/videos/)?(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // NavigateToString으로 페이지를 띄우면 origin이 없는(about:blank 성격) 문서가 되어
        // YouTube로 보내는 Referer가 비게 되고, 이는 최근 YouTube 정책 변경으로 인해
        // "오류 153(플레이어 구성 오류)"을 유발합니다. 이를 피하기 위해 실제 https 오리진을
        // 갖도록 로컬 폴더를 가상 호스트로 매핑해서 그 주소로 이동(Navigate)합니다.
        private const string VirtualHostName = "app.youtubelivedesktop.local";
        private const string WallpaperFileName = "wallpaper.html";

        // 로컬 동영상 파일을 서빙하기 위한 두 번째 가상 호스트. 선택한 파일이 바뀔 때마다
        // 해당 파일이 들어있는 폴더로 매핑을 다시 걸어줍니다.
        private const string LocalVirtualHostName = "local.youtubelivedesktop.local";

        private WebView2 _webView = null!;
        private bool _apiReady;
        private string _wallpaperHostDirectory = string.Empty;
        private readonly List<object> _pendingCommands = new();

        /// <summary>YouTube IFrame API가 준비되고 재생이 시작될 준비가 되었을 때 발생.</summary>
        public event Action? Ready;

        /// <summary>재생 오류 또는 WebView2 프로세스 오류 발생 시 메시지와 함께 발생.</summary>
        public event Action<string>? PlaybackError;

        public async Task InitializeAsync(WebView2 webView)
        {
            _webView = webView;

            var envOptions = new CoreWebView2EnvironmentOptions
            {
                // 저장된 설정이 "음소거 해제" 상태여도 사용자 제스처 없이 자동재생이
                // 유지되도록 명시합니다. 기본값은 항상 음소거로 시작해 정책 위반을 피합니다.
                AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
            };

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YouTubeLiveDesktop", "WebView2Data");

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, envOptions);
            await webView.EnsureCoreWebView2Async(env);

            // 배경 재생 페이지를 실제 https 오리진처럼 서빙하기 위한 가상 호스트 매핑.
            _wallpaperHostDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YouTubeLiveDesktop", "WallpaperHost");
            Directory.CreateDirectory(_wallpaperHostDirectory);

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHostName, _wallpaperHostDirectory, CoreWebView2HostResourceAccessKind.Deny);

            webView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                try
                {
                    HandleBridgeMessage(e.TryGetWebMessageAsString());
                }
                catch
                {
                    // JSON 형태로 온 메시지는 문자열 변환이 실패할 수 있으므로 무시합니다.
                }
            };

            webView.CoreWebView2.ProcessFailed += (_, e) =>
            {
                PlaybackError?.Invoke($"WebView2 프로세스 오류: {e.ProcessFailedKind}");
            };
        }

        private void HandleBridgeMessage(string? msg)
        {
            if (string.IsNullOrEmpty(msg)) return;

            if (msg == "ready")
            {
                _apiReady = true;
                Ready?.Invoke();
                FlushPendingCommands();
            }
            else if (msg.StartsWith("error:", StringComparison.Ordinal) ||
                     msg.StartsWith("fatal:", StringComparison.Ordinal))
            {
                PlaybackError?.Invoke(msg);
            }
        }

        /// <summary>
        /// 사용자가 입력한 값(YouTube/Vimeo URL 또는 로컬 파일 경로)을 해석합니다.
        /// 실제 존재하는 파일 경로면 로컬로, vimeo.com 링크면 Vimeo로, 그 외에는 YouTube로 처리합니다.
        /// 실패하면 null을 반환합니다.
        /// </summary>
        public async Task<PlaybackSource?> ResolveSourceAsync(string input)
        {
            var trimmed = input.Trim().Trim('"');
            if (string.IsNullOrEmpty(trimmed)) return null;

            // 1) 로컬 파일 경로: 실제로 존재하는 파일이면 무조건 로컬 재생으로 취급합니다.
            if (LooksLikeLocalFile(trimmed))
            {
                var url = MapLocalFileToVirtualHost(trimmed);
                return new PlaybackSource(SourceKind.Local, url, Path.GetFileName(trimmed));
            }

            // 2) Vimeo
            var vimeoMatch = VimeoIdRegex.Match(trimmed);
            if (vimeoMatch.Success)
            {
                var id = vimeoMatch.Groups[1].Value;
                return new PlaybackSource(SourceKind.Vimeo, id, $"Vimeo {id}");
            }

            // 3) YouTube (기존 방식과 동일하게 처리, 채널 handle 등은 리다이렉트를 따라갑니다)
            var videoId = await ResolveYouTubeVideoIdAsync(trimmed);
            if (videoId != null)
                return new PlaybackSource(SourceKind.YouTube, videoId, $"YouTube {videoId}");

            return null;
        }

        private static bool LooksLikeLocalFile(string input)
        {
            try
            {
                return File.Exists(input);
            }
            catch
            {
                // 너무 긴 문자열, 잘못된 문자 등으로 File.Exists가 예외를 던지는 경우가 있어 방어합니다.
                return false;
            }
        }

        private string MapLocalFileToVirtualHost(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            var folder = Path.GetDirectoryName(fullPath)!;
            var fileName = Path.GetFileName(fullPath);

            // 선택한 파일이 바뀔 때마다 그 폴더로 매핑을 다시 겁니다 (여러 번 호출해도 안전합니다).
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                LocalVirtualHostName, folder, CoreWebView2HostResourceAccessKind.Deny);

            // 같은 파일명을 다시 열 때 브라우저/미디어 캐시로 예전 내용이 뜨는 것을 막기 위해
            // 매번 다른 쿼리스트링을 붙여 강제로 새로 불러오게 합니다.
            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return $"https://{LocalVirtualHostName}/{Uri.EscapeDataString(fileName)}?t={cacheBuster}";
        }

        private async Task<string?> ResolveYouTubeVideoIdAsync(string url)
        {
            var direct = YouTubeIdRegex.Match(url);
            if (direct.Success)
                return direct.Groups[1].Value;

            if (_webView.CoreWebView2 == null)
                return null;

            var tcs = new TaskCompletionSource<string?>();

            void OnNavCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                _webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                var resolved = _webView.CoreWebView2.Source;
                var match = YouTubeIdRegex.Match(resolved ?? string.Empty);
                tcs.TrySetResult(match.Success ? match.Groups[1].Value : null);
            }

            _webView.CoreWebView2.NavigationCompleted += OnNavCompleted;
            _webView.CoreWebView2.Navigate(url);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            if (completed != tcs.Task)
            {
                _webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                return null;
            }

            return await tcs.Task;
        }

        /// <summary>지정한 소스를 16:9 유지 배경 페이지에 로드하고 재생을 시작합니다.</summary>
        public Task LoadSourceAsync(PlaybackSource source, int volume, bool muted, WeatherRegion? weatherRegion)
        {
            _apiReady = false;
            _pendingCommands.Clear();

            var html = BuildWallpaperHtml(source, volume, muted, weatherRegion);

            // NavigateToString 대신, 가상 호스트로 매핑한 실제 폴더에 파일을 써서
            // https 오리진을 가진 주소로 이동합니다 (YouTube 오류 153 방지, Referer 확보).
            var filePath = Path.Combine(_wallpaperHostDirectory, WallpaperFileName);
            File.WriteAllText(filePath, html);
            _webView.CoreWebView2.Navigate($"https://{VirtualHostName}/{WallpaperFileName}");

            return Task.CompletedTask;
        }

        public void SetVolume(int volume) => PostCommand(new { cmd = "setVolume", value = Math.Clamp(volume, 0, 100) });
        public void SetMuted(bool muted) => PostCommand(new { cmd = muted ? "mute" : "unmute" });
        public void Play() => PostCommand(new { cmd = "play" });
        public void Pause() => PostCommand(new { cmd = "pause" });

        /// <summary>
        /// 화면 중앙 날씨 표시 지역을 즉시 갱신합니다. 영상 재생 준비 여부와 무관하게 동작해야
        /// 하므로 PostCommand의 준비 대기 큐를 거치지 않고 바로 전송합니다.
        /// 좌표는 고정된 드롭다운 목록에서만 오므로 지오코딩 실패가 있을 수 없습니다.
        /// </summary>
        public void SetWeatherRegion(WeatherRegion? region)
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                var payload = region == null
                    ? new { cmd = "setRegion", name = "", lat = (double?)null, lon = (double?)null }
                    : new { cmd = "setRegion", name = region.Name, lat = (double?)region.Latitude, lon = (double?)region.Longitude };

                SendJson(payload);
            }
            catch
            {
                // 페이지가 아직 로드되지 않은 순간의 호출은 무시합니다.
            }
        }

        /// <summary>연결 끊김/오류 발생 시 같은 소스를 다시 로드합니다 (복구).</summary>
        public void Reload() => PostCommand(new { cmd = "reload" });

        /// <summary>문제 진단용 WebView2 개발자 도구(F12) 창을 엽니다.</summary>
        public void OpenDevTools() => _webView?.CoreWebView2?.OpenDevToolsWindow();

        /// <summary>
        /// 명령을 전송합니다. 플레이어(YouTube/Vimeo/로컬 video)가 아직 준비되지 않았다면
        /// 버리지 않고 큐에 쌓아뒀다가 'ready' 신호를 받는 즉시 순서대로 재전송합니다.
        /// (이전 버전은 준비 전 명령을 그냥 무시해, 앱을 켜자마자 음소거/볼륨을 조작하면
        /// 아무 반응이 없는 것처럼 보이는 문제가 있었습니다.)
        /// </summary>
        private void PostCommand(object command)
        {
            if (!_apiReady || _webView?.CoreWebView2 == null)
            {
                _pendingCommands.Add(command);
                return;
            }
            SendJson(command);
        }

        private void FlushPendingCommands()
        {
            if (_pendingCommands.Count == 0) return;
            var queued = new List<object>(_pendingCommands);
            _pendingCommands.Clear();
            foreach (var cmd in queued) SendJson(cmd);
        }

        private void SendJson(object command)
        {
            if (_webView?.CoreWebView2 == null) return;
            try
            {
                var json = JsonSerializer.Serialize(command);
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch
            {
                // WebView2가 아직 완전히 준비되지 않은 순간의 호출은 무시합니다.
            }
        }

        private static string BuildWallpaperHtml(PlaybackSource source, int volume, bool muted, WeatherRegion? weatherRegion)
        {
            // HTML/스크립트 문자열 안에 그대로 들어가므로 따옴표를 이스케이프합니다.
            static string EscapeJs(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

            var sourceType = source.Kind switch
            {
                SourceKind.YouTube => "youtube",
                SourceKind.Vimeo => "vimeo",
                SourceKind.Local => "local",
                _ => "youtube"
            };

            var regionName = weatherRegion?.Name ?? string.Empty;

            // JS 숫자 리터럴로 그대로 들어가므로 소수점 구분자가 로케일에 흔들리지 않도록 InvariantCulture를 씁니다.
            var lat = weatherRegion != null ? weatherRegion.Latitude.ToString(CultureInfo.InvariantCulture) : "null";
            var lon = weatherRegion != null ? weatherRegion.Longitude.ToString(CultureInfo.InvariantCulture) : "null";

            return GetEmbeddedTemplate()
                .Replace("__SOURCE_TYPE__", sourceType)
                .Replace("__SOURCE_VALUE__", EscapeJs(source.Value))
                .Replace("__VOLUME__", Math.Clamp(volume, 0, 100).ToString())
                .Replace("__MUTED__", muted ? "true" : "false")
                .Replace("__REGION_NAME__", EscapeJs(regionName))
                .Replace("__REGION_LAT__", lat)
                .Replace("__REGION_LON__", lon);
        }

        private static string GetEmbeddedTemplate()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("YouTubeLiveDesktop.Resources.wallpaper.html");
            if (stream == null)
                throw new FileNotFoundException("wallpaper.html 리소스를 찾을 수 없습니다. csproj의 EmbeddedResource 설정을 확인하세요.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
