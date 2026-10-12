using System.Configuration;
using System.Data;
using System.Windows;

namespace VRCGroupSwitcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    public App()
    {
        // ウィンドウ非表示時もバックグラウンドで動作を継続するため、明示的終了モードに設定
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (s, e) =>
        {
            string msg = $"予期しないエラーが発生しました: {e.Exception.Message}\n\n詳細:\n{e.Exception.StackTrace}";
            System.Windows.MessageBox.Show(msg, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                string msg = $"重大なエラー: {ex.Message}\n\n詳細:\n{ex.StackTrace}";
                System.Windows.MessageBox.Show(msg, "重大なエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            bool startInTray = false;
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-tray", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/tray", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startInTray = true;
                    break;
                }
            }

            _mainWindow = new MainWindow(startInTray);
            MainWindow = _mainWindow;

            if (!startInTray)
            {
                _mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"起動時エラー:\n{ex.Message}\n\n詳細:\n{ex.StackTrace}", "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

