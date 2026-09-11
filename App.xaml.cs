using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using YouTubeLiveDesktop.Models;
using YouTubeLiveDesktop.Services;
using YouTubeLiveDesktop.Views;

namespace YouTubeLiveDesktop
{
    /// <summary>
    /// 애플리케이션 진입점 겸 조립 지점 (Application Layer).
    /// 트레이 아이콘, 설정 창, 바탕화면(WallpaperWindow), 각 서비스 간의 이벤트를 연결합니다.
    /// </summary>
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;
        private SettingsService _settingsService = null!;
        private AppSettings _settings = null!;
        private StartupService _startupService = null!;
        private WallpaperWindow? _wallpaperWindow;
        private MainWindow? _mainWindow;
        private DonationWindow? _donationWindow;
        private TrayService _trayService = null!;
        private UpdateService _updateService = null!;
        private System.Timers.Timer? _updateTimer;
        private UpdateInfo? _pendingUpdate;
        private bool _fatalErrorNotified;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 원인 불명으로 프로세스가 아무 안내 없이 그냥 종료되는 문제(예: UI 스레드가 아닌
            // 백그라운드 스레드에서 발생한 미처리 예외)를 진단할 수 있도록, 어떤 스레드에서
            // 터지든 %AppData%\YouTubeLiveDesktop\crash.log 에 예외 내용을 남깁니다.
            // (UI 스레드 예외는 로그만 남기고 기본 동작대로 앱을 종료시킵니다 — 이미 오염된
            // 상태로 계속 실행하는 것보다 안전합니다.)
            AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);
            DispatcherUnhandledException += (_, args) => LogCrash(args.Exception);
            TaskScheduler.UnobservedTaskException += (_, args) => LogCrash(args.Exception);

            // 프로그램이 중복 실행되지 않도록 단일 인스턴스만 허용합니다.
            _singleInstanceMutex = new Mutex(true, "YouTubeLiveDesktop_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("YouTube Live Desktop이 이미 실행 중입니다. 시스템 트레이 아이콘을 확인해 주세요.",
                    "YouTube Live Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            _settingsService = new SettingsService();
            _settings = _settingsService.Load();
            _startupService = new StartupService();

            _trayService = new TrayService(_settings);
            WireTrayEvents();

            _wallpaperWindow = new WallpaperWindow();
            _wallpaperWindow.PlaybackError += OnPlaybackError;
            await _wallpaperWindow.InitializeAsync();
            _wallpaperWindow.SetWallpaperVisible(_settings.WallpaperEnabled);

            if (!string.IsNullOrWhiteSpace(_settings.LiveUrl) && _settings.WallpaperEnabled)
            {
                await ApplyUrlAsync(_settings.LiveUrl, showErrors: false);
            }
            else
            {
                // 처음 실행이거나 저장된 URL이 없으면 설정 창을 먼저 보여줍니다.
                ShowSettingsWindow();
            }

            StartUpdateChecks();
        }

        /// <summary>
        /// 시작 15초 후 한 번, 이후 6시간마다 GitHub Releases에서 새 버전이 있는지 확인합니다.
        /// (UpdateService.cs 상단에 GitHub 저장소 설정 방법이 적혀 있습니다.)
        /// </summary>
        private void StartUpdateChecks()
        {
            _updateService = new UpdateService();

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
                await CheckForUpdatesAsync();
            });

            _updateTimer = new System.Timers.Timer(TimeSpan.FromHours(6).TotalMilliseconds) { AutoReset = true };
            _updateTimer.Elapsed += async (_, _) => await CheckForUpdatesAsync();
            _updateTimer.Start();
        }

        private async Task CheckForUpdatesAsync()
        {
            var info = await _updateService.CheckForUpdateAsync();
            if (info == null)
            {
                // LastCheckError가 있으면 "새 버전 없음"이 아니라 실제 확인 실패라는 뜻이므로
                // 진단할 수 있도록 풍선 알림으로 원인을 보여줍니다(정상적으로 최신 버전인 경우엔 null).
                if (_updateService.LastCheckError is { } error)
                {
                    Dispatcher.Invoke(() =>
                        _trayService.ShowBalloon("YouTube Live Desktop", $"업데이트 확인 실패: {error}"));
                }
                return;
            }

            _pendingUpdate = info;
            Dispatcher.Invoke(() =>
            {
                _trayService.ShowUpdateAvailable(info.Version);
                _trayService.ShowBalloon("YouTube Live Desktop",
                    $"새 버전 v{info.Version}이 나왔습니다. 트레이 메뉴에서 클릭하면 자동으로 다운로드 후 설치됩니다.");
            });
        }

        private async void OnUpdateInstallRequested()
        {
            if (_pendingUpdate == null) return;
            var info = _pendingUpdate;

            try
            {
                _trayService.ShowBalloon("YouTube Live Desktop", "업데이트를 다운로드하는 중입니다...");
                var installerPath = await _updateService.DownloadInstallerAsync(info.DownloadUrl);

                _trayService.ShowBalloon("YouTube Live Desktop", "다운로드 완료. 설치를 시작합니다...");
                Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });

                // 설치 프로그램이 exe 파일을 덮어쓸 수 있도록 지금 실행 중인 앱을 종료합니다.
                Shutdown();
            }
            catch
            {
                MessageBox.Show(
                    "업데이트 다운로드 또는 설치 시작에 실패했습니다. 인터넷 연결을 확인한 뒤 다시 시도해 주세요.",
                    "YouTube Live Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void WireTrayEvents()
        {
            _trayService.PlayPauseRequested += () =>
            {
                _settings.IsPlaying = !_settings.IsPlaying;
                if (_settings.IsPlaying) _wallpaperWindow?.Play(); else _wallpaperWindow?.Pause();
                _trayService.UpdatePlayPauseLabel(_settings.IsPlaying);
                _settingsService.Save(_settings);
            };

            _trayService.MuteToggleRequested += muted =>
            {
                _settings.Muted = muted;
                _wallpaperWindow?.SetMuted(muted);
                _settingsService.Save(_settings);
                _mainWindow?.SyncMuted(muted);
            };

            _trayService.VolumeChanged += volume =>
            {
                _settings.Volume = volume;
                _wallpaperWindow?.SetVolume(volume);
                _trayService.UpdateVolume(volume);
                _settingsService.Save(_settings);
                _mainWindow?.SyncVolume(volume);
            };

            _trayService.OpenSettingsRequested += ShowSettingsWindow;

            _trayService.WallpaperToggleRequested += () =>
            {
                _settings.WallpaperEnabled = !_settings.WallpaperEnabled;
                _wallpaperWindow?.SetWallpaperVisible(_settings.WallpaperEnabled);
                _trayService.UpdateWallpaperEnabled(_settings.WallpaperEnabled);
                _settingsService.Save(_settings);
            };

            _trayService.OpenDevToolsRequested += () => _wallpaperWindow?.OpenDevTools();

            _trayService.DonationRequested += ShowDonationWindow;

            _trayService.UpdateInstallRequested += OnUpdateInstallRequested;

            _trayService.ExitRequested += Shutdown;
        }

        private void ShowSettingsWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_settings, _startupService);
                _mainWindow.ApplyRequested += async input => await ApplyUrlAsync(input, showErrors: true);
                _mainWindow.SettingsChanged += () => _settingsService.Save(_settings);
                _mainWindow.RegionApplyRequested += region =>
                {
                    _settings.WeatherRegion = region?.Name ?? string.Empty;
                    _wallpaperWindow?.SetWeatherRegion(region);
                    _settingsService.Save(_settings);
                };
                _mainWindow.HistoryReopenRequested += async entry => await ApplyUrlAsync(entry.RawInput, showErrors: true);

                _mainWindow.HistoryDeleteRequested += entries =>
                {
                    foreach (var entry in entries)
                        _settings.History.RemoveAll(h => h.RawInput == entry.RawInput);
                    _settingsService.Save(_settings);
                    _mainWindow?.RefreshHistory(_settings.History);
                };

                // 설정 창의 음소거 체크박스/볼륨 슬라이더는 값을 저장만 하고 실제 재생에는
                // 반영하지 않던 것이 "음소거/볼륨 버튼이 작동하지 않는다"는 문제의 원인 중
                // 하나였습니다. 트레이 메뉴와 동일하게 즉시 재생에 반영하고 트레이 상태도 맞춥니다.
                _mainWindow.MuteChanged += muted =>
                {
                    _wallpaperWindow?.SetMuted(muted);
                    _trayService.UpdateMuted(muted);
                };
                _mainWindow.VolumeChanged += volume =>
                {
                    _wallpaperWindow?.SetVolume(volume);
                    _trayService.UpdateVolume(volume);
                };
            }

            _mainWindow.RefreshHistory(_settings.History);
            _mainWindow.Show();
            _mainWindow.Activate();
        }

        private void ShowDonationWindow()
        {
            if (_donationWindow == null)
            {
                _donationWindow = new DonationWindow();
                _donationWindow.Closed += (_, _) => _donationWindow = null;
            }

            _donationWindow.Show();
            _donationWindow.Activate();
        }

        private async Task ApplyUrlAsync(string input, bool showErrors)
        {
            if (_wallpaperWindow == null) return;

            var region = WeatherRegions.Find(_settings.WeatherRegion);
            var source = await _wallpaperWindow.PlayUrlAsync(input, _settings.Volume, _settings.Muted, region);
            if (source != null)
            {
                _fatalErrorNotified = false;
                _settings.LiveUrl = input;
                _settings.IsPlaying = true;
                _settings.WallpaperEnabled = true;
                _wallpaperWindow.SetWallpaperVisible(true);
                _trayService.UpdateWallpaperEnabled(true);
                AddToHistory(input, source);
                _settingsService.Save(_settings);
                _mainWindow?.SetStatus($"재생 중 ({KindLabel(source.Kind)})");
                _mainWindow?.RefreshHistory(_settings.History);
            }
            else if (showErrors)
            {
                MessageBox.Show(
                    "입력한 URL/파일에서 재생할 영상을 찾지 못했습니다.\nYouTube·Vimeo URL이거나 실제 존재하는 로컬 동영상 파일 경로인지 확인해 주세요.",
                    "YouTube Live Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
                _mainWindow?.SetStatus("URL/파일 오류");
            }
        }

        private void AddToHistory(string rawInput, PlaybackSource source)
        {
            _settings.History.RemoveAll(h => h.RawInput == rawInput);
            _settings.History.Insert(0, new HistoryEntry
            {
                RawInput = rawInput,
                DisplayName = source.DisplayName,
                Kind = source.Kind.ToString(),
                LastUsedUtc = DateTime.UtcNow
            });

            const int maxHistory = 20;
            if (_settings.History.Count > maxHistory)
                _settings.History.RemoveRange(maxHistory, _settings.History.Count - maxHistory);
        }

        private static string KindLabel(SourceKind kind) => kind switch
        {
            SourceKind.YouTube => "YouTube",
            SourceKind.Vimeo => "Vimeo",
            SourceKind.Local => "로컬 파일",
            _ => kind.ToString()
        };

        private void OnPlaybackError(string message)
        {
            var isFatal = message.StartsWith("fatal:", StringComparison.Ordinal);

            if (isFatal)
            {
                // 임베드 차단/영상 없음 등 재시도로 해결되지 않는 오류입니다.
                // wallpaper.html이 이미 화면에 안내 문구를 표시하므로, 여기서는
                // (반복 팝업 없이) 트레이 풍선 알림으로 한 번만 알려줍니다.
                _mainWindow?.SetStatus("재생 불가 - 다른 URL을 입력해 주세요");
                if (!_fatalErrorNotified)
                {
                    _fatalErrorNotified = true;
                    _trayService.ShowBalloon("YouTube Live Desktop",
                        "재생할 수 없습니다 (임베드 제한, 파일 없음 등). 설정에서 다른 URL/파일을 입력해 주세요.");
                }
            }
            else
            {
                _mainWindow?.SetStatus("일시적 오류 - 재시도 중...");
            }
        }

        private static void LogCrash(Exception? ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YouTubeLiveDesktop");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch
            {
                // 로그 남기기 자체가 실패해도(디스크 오류 등) 앱 종료 흐름을 막지는 않습니다.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _wallpaperWindow?.ShutdownWallpaper();
            _trayService?.Dispose();
            _singleInstanceMutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
