using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VRCGroupSwitcher.ViewModels;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using FlowDirection = System.Windows.FlowDirection;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace VRCGroupSwitcher.Services
{
    public static class MockPreviewGenerator
    {
        public static string GenerateDashboardScreenshot(string outputPath, int width = 1080, int height = 720)
        {
            string bannerPath = CreateMockBannerFile();
            string avatarPath = CreateMockAvatarFile();

            var mockVm = MainViewModel.CreateMockViewModel(bannerPath, avatarPath);

            var window = new MainWindow(mockVm, isMockMode: true)
            {
                Width = width,
                Height = height,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false
            };

            // ウィンドウを表示してWPF描画パイプラインを確実に実行
            window.Show();

            // レイアウト強制更新
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
            window.UpdateLayout();

            // レンダーターゲットビットマップ
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(window);

            window.Close();

            // 保存先ディレクトリ確認
            string fullOutputPath = Path.GetFullPath(outputPath);
            string? dir = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // PNGとしてエンコード＆書き込み
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var stream = File.Create(fullOutputPath))
            {
                encoder.Save(stream);
            }

            return fullOutputPath;
        }

        private static string CreateMockBannerFile()
        {
            int w = 800;
            int h = 200;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                // ダークサイバーグラデーション背景
                var bg = new LinearGradientBrush(
                    Color.FromRgb(15, 23, 42),
                    Color.FromRgb(14, 116, 144),
                    new Point(0, 0),
                    new Point(1, 1));
                dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));

                // グリッド装飾ライン
                var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)), 1.5);
                for (int x = 0; x < w; x += 50)
                {
                    dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
                }
                for (int y = 0; y < h; y += 50)
                {
                    dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
                }

                // ネオンアクセントサークル
                var circleBrush = new RadialGradientBrush(
                    Color.FromArgb(90, 56, 189, 248),
                    Color.FromArgb(0, 15, 23, 42));
                dc.DrawEllipse(circleBrush, null, new Point(w * 0.75, h * 0.5), 180, 180);

                // バナーテキスト
                var ft = new FormattedText(
                    "TEST GROUP",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    36,
                    new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    96.0);
                dc.DrawText(ft, new Point(50, (h - ft.Height) / 2));

                var subFt = new FormattedText(
                    "TEST / DEMO COMMUNITY [TEST.0000]",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                    16,
                    new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    96.0);
                dc.DrawText(subFt, new Point(52, ((h - ft.Height) / 2) + ft.Height + 4));
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            string tempFile = Path.Combine(Path.GetTempPath(), "vrc_mock_banner.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(tempFile))
            {
                encoder.Save(fs);
            }
            return tempFile;
        }

        private static string CreateMockAvatarFile()
        {
            int size = 128;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bg = new LinearGradientBrush(
                    Color.FromRgb(56, 189, 248),
                    Color.FromRgb(99, 102, 241),
                    new Point(0, 0),
                    new Point(1, 1));
                dc.DrawEllipse(bg, null, new Point(size / 2.0, size / 2.0), size / 2.0, size / 2.0);

                var ft = new FormattedText(
                    "👤",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Emoji"),
                    54,
                    Brushes.White,
                    96.0);
                dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            string tempFile = Path.Combine(Path.GetTempPath(), "vrc_mock_avatar.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(tempFile))
            {
                encoder.Save(fs);
            }
            return tempFile;
        }
    }
}
