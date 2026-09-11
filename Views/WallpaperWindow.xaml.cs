using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using YouTubeLiveDesktop.Models;
using YouTubeLiveDesktop.Services;

namespace YouTubeLiveDesktop.Views
{
    /// <summary>
    /// 실제 YouTube Live 영상을 담아 바탕화면 배경 레이어에 삽입되는 창입니다.
    /// 일반 최상위 창이 아니라 WorkerW의 자식 창으로 재배치되어 동작합니다.
    /// </summary>
    public partial class WallpaperWindow : Window
    {
        private readonly WebViewService _webViewService = new();
        private readonly DesktopWallpaperService _desktopWallpaperService = new();

        public event Action<string>? PlaybackError;

        public WallpaperWindow()
        {
            InitializeComponent();
            PositionToPrimaryScreen();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private void PositionToPrimaryScreen()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return;

            Left = screen.Bounds.Left;
            Top = screen.Bounds.Top;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;
        }

        public async Task InitializeAsync()
        {
            // WebView2 초기화에는 유효한 창 핸들이 필요하므로 먼저 표시합니다.
            Show();

            await _webViewService.InitializeAsync(WebView);
            // 재시도는 wallpaper.html(JS) 쪽에서 오류 종류에 따라 제한된 횟수만 수행합니다.
            // 여기서 C#이 추가로 즉시 재로드를 호출하면 두 재시도 로직이 겹쳐 화면이
            // 빠르게 깜빡이는 원인이 되므로, 이 쪽에서는 상태 표시용으로만 전달합니다.
            _webViewService.PlaybackError += msg => Dispatcher.Invoke(() => PlaybackError?.Invoke(msg));

            EmbedIntoDesktop();
        }

        private void EmbedIntoDesktop()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            _desktopWallpaperService.Embed(hwnd);

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen != null)
            {
                _desktopWallpaperService.Resize(hwnd, screen.Bounds.Left, screen.Bounds.Top,
                    screen.Bounds.Width, screen.Bounds.Height);
            }
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return;

                var hwnd = new WindowInteropHelper(this).Handle;
                _desktopWallpaperService.Resize(hwnd, screen.Bounds.Left, screen.Bounds.Top,
                    screen.Bounds.Width, screen.Bounds.Height);
            });
        }

        /// <summary>
        /// 입력값(YouTube/Vimeo URL 또는 로컬 파일 경로)을 해석해 재생을 시작합니다.
        /// 성공하면 해석된 소스 정보를(히스토리 기록 등에 사용), 실패하면 null을 반환합니다.
        /// </summary>
        public async Task<PlaybackSource?> PlayUrlAsync(string input, int volume, bool muted, WeatherRegion? weatherRegion)
        {
            var source = await _webViewService.ResolveSourceAsync(input);
            if (source == null)
                return null;

            await _webViewService.LoadSourceAsync(source, volume, muted, weatherRegion);
            return source;
        }

        public void SetVolume(int volume) => _webViewService.SetVolume(volume);
        public void SetMuted(bool muted) => _webViewService.SetMuted(muted);
        public void Play() => _webViewService.Play();
        public void Pause() => _webViewService.Pause();
        public void Reload() => _webViewService.Reload();
        public void OpenDevTools() => _webViewService.OpenDevTools();

        /// <summary>화면 중앙에 표시되는 날씨의 조회 지역을 다시 로드 없이 즉시 갱신합니다.</summary>
        public void SetWeatherRegion(WeatherRegion? region) => _webViewService.SetWeatherRegion(region);

        public void SetWallpaperVisible(bool visible) =>
            Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 창을 닫아 배경 레이어에서 제거합니다. 창이 사라지면 Windows Desktop은
        /// 자동으로 원래의 정적 배경화면 상태로 돌아갑니다 (완료 기준: 종료 시 원상 복구).
        /// </summary>
        public void ShutdownWallpaper()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            Close();
        }
    }
}
