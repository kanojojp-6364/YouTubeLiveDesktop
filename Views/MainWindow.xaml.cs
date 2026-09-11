using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YouTubeLiveDesktop.Models;
using YouTubeLiveDesktop.Services;

namespace YouTubeLiveDesktop.Views
{
    /// <summary>
    /// 메인 설정 화면: URL 입력/적용/저장, 볼륨, 음소거, 자동실행, 재생 기록 (UI Layer).
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly StartupService _startupService;
        private bool _isLoadingUi = true;

        /// <summary>적용 버튼 클릭 시 발생. 반환된 Task 완료를 기다려 상태를 갱신합니다.</summary>
        public event Func<string, Task>? ApplyRequested;

        /// <summary>볼륨/음소거/자동실행 등 값이 바뀌어 저장이 필요할 때 발생.</summary>
        public event Action? SettingsChanged;

        /// <summary>날씨 지역 적용 버튼 클릭 시 발생 (선택한 지역, 목록에 없으면 null).</summary>
        public event Action<WeatherRegion?>? RegionApplyRequested;

        /// <summary>설정 창의 음소거 체크박스를 직접 조작했을 때 발생 (실제 재생에 즉시 반영하기 위함).</summary>
        public event Action<bool>? MuteChanged;

        /// <summary>설정 창의 볼륨 슬라이더를 직접 조작했을 때 발생 (실제 재생에 즉시 반영하기 위함).</summary>
        public event Action<int>? VolumeChanged;

        /// <summary>재생 기록 항목을 더블클릭해 다시 열도록 요청했을 때 발생.</summary>
        public event Action<HistoryEntry>? HistoryReopenRequested;

        /// <summary>재생 기록 중 일부(체크한 항목) 또는 전체를 삭제하도록 요청했을 때 발생.</summary>
        public event Action<List<HistoryEntry>>? HistoryDeleteRequested;

        public MainWindow(AppSettings settings, StartupService startupService)
        {
            InitializeComponent();
            _settings = settings;
            _startupService = startupService;

            UrlTextBox.Text = _settings.LiveUrl;
            VolumeSlider.Value = _settings.Volume;
            VolumeValueText.Text = _settings.Volume.ToString();
            MuteCheckBox.IsChecked = _settings.Muted;
            AutoStartCheckBox.IsChecked = _startupService.IsEnabled();

            if (!_startupService.IsRunningFromPublishedExe())
                AutoStartWarningText.Visibility = Visibility.Visible;

            // 자유 입력 대신 좌표가 확정된 목록에서만 고르게 해, 지역명 오타로 날씨가
            // 안 뜨는 문제가 애초에 생기지 않도록 합니다.
            RegionComboBox.ItemsSource = Array.ConvertAll(WeatherRegions.All, r => r.Name);
            RegionComboBox.SelectedItem = _settings.WeatherRegion;

            RefreshHistory(_settings.History);

            _isLoadingUi = false;
        }

        public void SetStatus(string status) => Dispatcher.Invoke(() => StatusText.Text = status);

        /// <summary>재생 기록 목록을 새로 고칩니다(최신순). 어느 스레드에서 호출해도 안전합니다.</summary>
        public void RefreshHistory(List<HistoryEntry> history)
        {
            Dispatcher.Invoke(() =>
            {
                HistoryListBox.ItemsSource = null;
                HistoryListBox.ItemsSource = history;
            });
        }

        /// <summary>트레이 등 다른 경로에서 음소거 상태가 바뀌었을 때, 이벤트를 재발생시키지 않고 체크박스만 맞춥니다.</summary>
        public void SyncMuted(bool muted)
        {
            Dispatcher.Invoke(() =>
            {
                var wasLoading = _isLoadingUi;
                _isLoadingUi = true;
                MuteCheckBox.IsChecked = muted;
                _isLoadingUi = wasLoading;
            });
        }

        /// <summary>트레이 등 다른 경로에서 볼륨이 바뀌었을 때, 이벤트를 재발생시키지 않고 슬라이더만 맞춥니다.</summary>
        public void SyncVolume(int volume)
        {
            Dispatcher.Invoke(() =>
            {
                var wasLoading = _isLoadingUi;
                _isLoadingUi = true;
                VolumeSlider.Value = volume;
                VolumeValueText.Text = volume.ToString();
                _isLoadingUi = wasLoading;
            });
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("YouTube/Vimeo URL 또는 로컬 동영상 파일 경로를 입력해 주세요.", "YouTube Live Desktop");
                return;
            }

            StatusText.Text = "적용 중...";
            if (ApplyRequested != null)
                await ApplyRequested.Invoke(url);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "재생할 동영상 파일 선택",
                Filter = "동영상 파일|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.wmv;*.m4v|모든 파일|*.*"
            };

            if (dialog.ShowDialog(this) == true)
            {
                UrlTextBox.Text = dialog.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.LiveUrl = UrlTextBox.Text.Trim();
            SettingsChanged?.Invoke();
            StatusText.Text = "저장됨";
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingUi) return;
            var value = (int)e.NewValue;
            VolumeValueText.Text = value.ToString();
            _settings.Volume = value;
            VolumeChanged?.Invoke(value);
            SettingsChanged?.Invoke();
        }

        private void MuteCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi) return;
            var muted = MuteCheckBox.IsChecked == true;
            _settings.Muted = muted;
            MuteChanged?.Invoke(muted);
            SettingsChanged?.Invoke();
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi) return;
            var enabled = AutoStartCheckBox.IsChecked == true;
            var success = _startupService.SetEnabled(enabled);

            if (enabled && !success)
            {
                MessageBox.Show(
                    "자동 실행 등록에 실패했습니다. 빌드된 실행 파일(exe)을 직접 실행한 상태에서 다시 시도해 주세요.\n" +
                    "(Visual Studio의 '디버그 시작' 등 개발 모드에서는 등록할 수 없습니다.)",
                    "YouTube Live Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);

                var wasLoading = _isLoadingUi;
                _isLoadingUi = true;
                AutoStartCheckBox.IsChecked = false;
                _isLoadingUi = wasLoading;
                _settings.AutoStart = false;
            }
            else
            {
                _settings.AutoStart = enabled;
            }

            SettingsChanged?.Invoke();
        }

        private void RegionApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var name = RegionComboBox.SelectedItem as string ?? string.Empty;
            var region = WeatherRegions.Find(name);

            _settings.WeatherRegion = name;
            SettingsChanged?.Invoke();
            RegionApplyRequested?.Invoke(region);
        }

        private void HistoryListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryListBox.SelectedItem is HistoryEntry entry)
            {
                UrlTextBox.Text = entry.RawInput;
                HistoryReopenRequested?.Invoke(entry);
            }
        }

        private void DeleteSelectedHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryListBox.ItemsSource is not IEnumerable<HistoryEntry> items) return;

            var toDelete = items.Where(h => h.IsChecked).ToList();
            if (toDelete.Count == 0)
            {
                MessageBox.Show("삭제할 항목의 체크박스를 먼저 선택해 주세요.", "YouTube Live Desktop");
                return;
            }

            HistoryDeleteRequested?.Invoke(toDelete);
        }

        private void DeleteAllHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryListBox.ItemsSource is not IEnumerable<HistoryEntry> items) return;

            var all = items.ToList();
            if (all.Count == 0) return;

            var result = MessageBox.Show("재생 기록을 모두 삭제하시겠습니까?", "YouTube Live Desktop",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                HistoryDeleteRequested?.Invoke(all);
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e) => Hide();

        protected override void OnClosing(CancelEventArgs e)
        {
            // 설정 창을 닫아도 프로그램은 트레이에서 계속 동작해야 하므로 숨기기만 합니다.
            // 완전 종료는 트레이 메뉴의 '종료'(Application.Shutdown)를 통해서만 이루어집니다.
            e.Cancel = true;
            Hide();
        }
    }
}
