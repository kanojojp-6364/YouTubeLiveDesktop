using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using YouTubeLiveDesktop.Models;

namespace YouTubeLiveDesktop.Views
{
    /// <summary>
    /// 개발자 후원(코인 입금 주소 + QR코드) 안내 창입니다.
    /// 주소는 Models\DonationAddress.cs에 고정되어 있고, QR코드는 외부 서비스 없이
    /// 앱 안에서 직접 생성합니다(네트워크 통신 없음, QRCoder 라이브러리 사용).
    /// </summary>
    public partial class DonationWindow : Window
    {
        public DonationWindow()
        {
            InitializeComponent();
            DeveloperInfoText.Text = $"제작자: {DeveloperInfo.Nickname}  ·  {DeveloperInfo.Email}";
            BuildTabs();
        }

        private void BuildTabs()
        {
            foreach (var coin in DonationAddresses.All)
            {
                var panel = new StackPanel { Margin = new Thickness(16) };

                panel.Children.Add(new TextBlock
                {
                    Text = $"{coin.CoinName} ({coin.Network})",
                    FontWeight = FontWeights.Bold,
                    FontSize = 15,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                panel.Children.Add(new Image
                {
                    Source = GenerateQrImage(coin.Address),
                    Width = 200,
                    Height = 200,
                    Margin = new Thickness(0, 0, 0, 16),
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                panel.Children.Add(new TextBlock
                {
                    Text = "입금 주소",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                panel.Children.Add(BuildCopyRow(coin.Address));

                if (!string.IsNullOrEmpty(coin.Tag))
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = "데스티네이션 태그 (Destination Tag)",
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 14, 0, 4)
                    });
                    panel.Children.Add(BuildCopyRow(coin.Tag));

                    panel.Children.Add(new TextBlock
                    {
                        Text = "⚠ 주소와 데스티네이션 태그를 모두 정확히 입력해야 정상적으로 입금됩니다.",
                        Foreground = Brushes.OrangeRed,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 11,
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                }

                var tab = new TabItem
                {
                    Header = coin.CoinName,
                    Content = new ScrollViewer
                    {
                        Content = panel,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    }
                };

                MainTabControl.Items.Add(tab);
            }
        }

        /// <summary>주소 텍스트 + "복사" 버튼 한 줄을 만듭니다.</summary>
        private static UIElement BuildCopyRow(string text)
        {
            var dock = new DockPanel();

            var copyButton = new Button { Content = "복사", Width = 56, Height = 26 };
            copyButton.Click += (_, _) =>
            {
                try { Clipboard.SetText(text); }
                catch
                {
                    // 다른 프로그램이 클립보드를 잠깐 점유 중인 경우 등은 조용히 무시합니다.
                }
            };
            DockPanel.SetDock(copyButton, Dock.Right);
            dock.Children.Add(copyButton);

            var textBox = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(4),
                VerticalContentAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            dock.Children.Add(textBox);

            return dock;
        }

        /// <summary>주어진 문자열의 QR코드를 PNG 비트맵으로 생성합니다(외부 서비스 호출 없음).</summary>
        private static BitmapImage GenerateQrImage(string content)
        {
            // QRCodeGenerator/QRCodeData는 버전에 따라 IDisposable 구현 여부가 달라
            // 안전하게 using 없이 사용합니다(순수 관리 메모리라 명시적 해제가 꼭 필요하지 않습니다).
            var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var pngQr = new PngByteQRCode(data);
            var bytes = pngQr.GetGraphic(8);

            var image = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
