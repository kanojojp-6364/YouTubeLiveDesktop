using System;
using System.Drawing;
using System.Windows.Forms;
using YouTubeLiveDesktop.Models;

namespace YouTubeLiveDesktop.Services
{
    /// <summary>
    /// 시스템 트레이 아이콘과 우클릭 메뉴(재생/일시정지, 음소거, 볼륨, 설정, 배경 비활성화, 종료)를 담당합니다.
    /// WPF에는 트레이 아이콘 API가 없으므로 System.Windows.Forms.NotifyIcon을 사용합니다
    /// (csproj의 UseWindowsForms=true 로 참조 가능).
    ///
    /// 참고: 이전 버전은 볼륨 조절에 ContextMenuStrip 안에 TrackBar(드래그형 슬라이더)를
    /// 넣었었는데, Windows 트레이 팝업 메뉴 안에서는 마우스 캡처가 메뉴와 충돌해
    /// 드래그가 먹히지 않거나 메뉴가 먼저 닫혀버리는 경우가 흔합니다(잘 알려진 WinForms 제약).
    /// 그래서 클릭 한 번으로 확정되는 메뉴 항목(사전 설정값 + 증감 버튼) 방식으로 바꿔
    /// 항상 동작하도록 했습니다. 세밀한 조절은 설정 창의 슬라이더를 사용하면 됩니다.
    /// </summary>
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _playPauseItem;
        private readonly ToolStripMenuItem _muteItem;
        private readonly ToolStripMenuItem _wallpaperToggleItem;
        private readonly ToolStripMenuItem _updateItem;
        private readonly ToolStripSeparator _updateSeparator;
        private int _currentVolume;

        public event Action? PlayPauseRequested;
        public event Action<bool>? MuteToggleRequested;
        public event Action<int>? VolumeChanged;
        public event Action? OpenSettingsRequested;
        public event Action? WallpaperToggleRequested;
        public event Action? OpenDevToolsRequested;
        public event Action? DonationRequested;
        public event Action? UpdateInstallRequested;
        public event Action? ExitRequested;

        public TrayService(AppSettings settings)
        {
            _currentVolume = Math.Clamp(settings.Volume, 0, 100);

            var menu = new ContextMenuStrip();

            // 새 버전이 있을 때만 나타나는 항목입니다(평소에는 숨김). 굵은 글씨로 눈에 띄게 표시합니다.
            _updateItem = new ToolStripMenuItem("업데이트 확인 중...")
            {
                Visible = false,
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            _updateItem.Click += (_, _) => UpdateInstallRequested?.Invoke();
            menu.Items.Add(_updateItem);

            _updateSeparator = new ToolStripSeparator { Visible = false };
            menu.Items.Add(_updateSeparator);

            _playPauseItem = new ToolStripMenuItem(settings.IsPlaying ? "일시정지" : "재생");
            _playPauseItem.Click += (_, _) => PlayPauseRequested?.Invoke();
            menu.Items.Add(_playPauseItem);

            _muteItem = new ToolStripMenuItem("음소거") { CheckOnClick = true, Checked = settings.Muted };
            _muteItem.Click += (_, _) => MuteToggleRequested?.Invoke(_muteItem.Checked);
            menu.Items.Add(_muteItem);

            var volumeMenu = new ToolStripMenuItem("볼륨");
            foreach (var preset in new[] { 0, 25, 50, 75, 100 })
            {
                var presetItem = new ToolStripMenuItem(preset + "%");
                presetItem.Click += (_, _) => VolumeChanged?.Invoke(preset);
                volumeMenu.DropDownItems.Add(presetItem);
            }
            volumeMenu.DropDownItems.Add(new ToolStripSeparator());

            var volumeUpItem = new ToolStripMenuItem("볼륨 크게 (+10%)");
            volumeUpItem.Click += (_, _) => VolumeChanged?.Invoke(Math.Clamp(_currentVolume + 10, 0, 100));
            volumeMenu.DropDownItems.Add(volumeUpItem);

            var volumeDownItem = new ToolStripMenuItem("볼륨 작게 (-10%)");
            volumeDownItem.Click += (_, _) => VolumeChanged?.Invoke(Math.Clamp(_currentVolume - 10, 0, 100));
            volumeMenu.DropDownItems.Add(volumeDownItem);

            menu.Items.Add(volumeMenu);

            menu.Items.Add(new ToolStripSeparator());

            var openSettings = new ToolStripMenuItem("설정 열기");
            openSettings.Click += (_, _) => OpenSettingsRequested?.Invoke();
            menu.Items.Add(openSettings);

            var donationItem = new ToolStripMenuItem("개발자 후원하기");
            donationItem.Click += (_, _) => DonationRequested?.Invoke();
            menu.Items.Add(donationItem);

            _wallpaperToggleItem = new ToolStripMenuItem("바탕화면 배경 비활성화")
            {
                CheckOnClick = true,
                Checked = !settings.WallpaperEnabled
            };
            _wallpaperToggleItem.Click += (_, _) => WallpaperToggleRequested?.Invoke();
            menu.Items.Add(_wallpaperToggleItem);

            var devToolsItem = new ToolStripMenuItem("개발자 도구 열기 (문제 진단용)");
            devToolsItem.Click += (_, _) => OpenDevToolsRequested?.Invoke();
            menu.Items.Add(devToolsItem);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("종료");
            exit.Click += (_, _) => ExitRequested?.Invoke();
            menu.Items.Add(exit);

            _notifyIcon = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Visible = true,
                // NotifyIcon.Text는 63자 제한이 있어 짧게 표기합니다.
                Text = $"YouTube Live Desktop (by {DeveloperInfo.Nickname})",
                ContextMenuStrip = menu
            };
            _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
        }

        /// <summary>
        /// 트레이 아이콘 이미지를 exe에 내장된 Resources\app.ico 에서 읽어옵니다.
        /// 아이콘을 바꾸고 싶으면 이 파일만 교체하면 exe 아이콘/바로가기 아이콘/트레이 아이콘이
        /// 모두 함께 바뀝니다 (csproj의 ApplicationIcon도 같은 파일을 가리킵니다).
        /// </summary>
        private static Icon LoadTrayIcon()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("YouTubeLiveDesktop.Resources.app.ico");
                if (stream != null) return new Icon(stream);
            }
            catch
            {
                // 리소스 로드 실패 시에도 트레이 아이콘 자체가 없어서 앱이 죽는 일은 없도록
                // 기본 아이콘으로 대체합니다.
            }
            return SystemIcons.Application;
        }

        public void UpdatePlayPauseLabel(bool isPlaying) => _playPauseItem.Text = isPlaying ? "일시정지" : "재생";
        public void UpdateMuted(bool muted) => _muteItem.Checked = muted;
        public void UpdateVolume(int volume) => _currentVolume = Math.Clamp(volume, 0, 100);
        public void UpdateWallpaperEnabled(bool enabled) => _wallpaperToggleItem.Checked = !enabled;

        /// <summary>새 버전이 감지되면 호출해 트레이 메뉴 맨 위에 업데이트 항목을 표시합니다.</summary>
        public void ShowUpdateAvailable(string version)
        {
            _updateItem.Text = $"⬆ 새 버전 사용 가능 (v{version}) - 클릭해서 설치";
            _updateItem.Visible = true;
            _updateSeparator.Visible = true;
        }

        public void ShowBalloon(string title, string text) =>
            _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
