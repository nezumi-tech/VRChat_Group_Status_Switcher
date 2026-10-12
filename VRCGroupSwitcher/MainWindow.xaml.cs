using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VRCGroupSwitcher.Models;
using VRCGroupSwitcher.Services;
using VRCGroupSwitcher.ViewModels;

namespace VRCGroupSwitcher
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly VRChatApiClient _apiClient;
        private readonly VRChatLogWatcher _logWatcher;
        private readonly SettingsService _settingsService;
        private readonly SwitchingService _switchingService;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExplicitExit = false;

        private bool _isInitialized = false;

        public MainWindow() : this(false)
        {
        }

        public MainWindow(bool startInTray)
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            _apiClient = new VRChatApiClient();
            _logWatcher = new VRChatLogWatcher
            {
                CustomDirectoryPath = _settingsService.CurrentSettings.CustomLogDirectory
            };
            _switchingService = new SwitchingService(_apiClient, _logWatcher, _settingsService);

            _viewModel = new MainViewModel(_apiClient, _logWatcher, _settingsService, _switchingService);
            DataContext = _viewModel;

            InitializeTrayIcon();

            Closed += MainWindow_Closed;

            if (startInTray)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                Hide();

                // startInTray時はLoadedイベントが発火しないため、直ちに初期化を実行
                _ = EnsureInitializedAsync();
            }
            else
            {
                Loaded += async (s, e) => await EnsureInitializedAsync();
            }
        }

        private async System.Threading.Tasks.Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            await _viewModel.InitializeAsync();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "VRC Group Switcher",
                Visible = true
            };

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = System.IO.Path.Combine(baseDir, "app.ico");
                if (System.IO.File.Exists(icoPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(icoPath);
                }
                else
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                    {
                        _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                    else
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            // Context Menu
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();

            var titleItem = new System.Windows.Forms.ToolStripMenuItem($"VRC Group Switcher v{_viewModel.AppVersion}")
            {
                Enabled = false
            };
            contextMenu.Items.Add(titleItem);
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var showItem = new System.Windows.Forms.ToolStripMenuItem("ウィンドウを表示 (&O)");
            showItem.Click += (s, e) => ShowAndActivate();
            contextMenu.Items.Add(showItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var autoSwitchItem = new System.Windows.Forms.ToolStripMenuItem("自動切り替え (&A)")
            {
                CheckOnClick = true,
                Checked = _viewModel.AutoSwitchEnabled
            };
            autoSwitchItem.Click += (s, e) =>
            {
                _viewModel.AutoSwitchEnabled = autoSwitchItem.Checked;
            };
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.AutoSwitchEnabled))
                {
                    autoSwitchItem.Checked = _viewModel.AutoSwitchEnabled;
                }
                UpdateTrayTooltip();
            };
            contextMenu.Items.Add(autoSwitchItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("終了 (&X)");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // 左クリックまたはダブルクリックでウィンドウを表示
            _notifyIcon.DoubleClick += (s, e) => ShowAndActivate();
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    ShowAndActivate();
                }
            };
        }

        private void UpdateTrayTooltip()
        {
            if (_notifyIcon == null) return;
            try
            {
                string text = $"VRC Group Switcher\n{_viewModel.DisplayName} ({_viewModel.UserStatus})";
                if (text.Length > 63)
                {
                    text = text.Substring(0, 60) + "...";
                }
                _notifyIcon.Text = text;
            }
            catch
            {
                // Ignore text overflow
            }
        }

        public void ShowAndActivate()
        {
            Show();
            ShowInTaskbar = true;
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
        }

        public void ExitApplication()
        {
            _isExplicitExit = true;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            {
                ShowInTaskbar = false;
                Hide();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExplicitExit && _viewModel.CloseToTray)
            {
                e.Cancel = true;
                ShowInTaskbar = false;
                Hide();
                return;
            }

            base.OnClosing(e);
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _switchingService.Dispose();
            _logWatcher.Dispose();
            _apiClient.Dispose();
        }

        #region Navigation
        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (TabDashboardView == null) return;

            TabDashboardView.Visibility = Visibility.Collapsed;
            TabGroupsView.Visibility = Visibility.Collapsed;
            TabRulesView.Visibility = Visibility.Collapsed;
            TabSettingsView.Visibility = Visibility.Collapsed;
            TabLogsView.Visibility = Visibility.Collapsed;

            if (NavDashboard.IsChecked == true) TabDashboardView.Visibility = Visibility.Visible;
            else if (NavGroups.IsChecked == true) TabGroupsView.Visibility = Visibility.Visible;
            else if (NavRules.IsChecked == true) TabRulesView.Visibility = Visibility.Visible;
            else if (NavSettings.IsChecked == true) TabSettingsView.Visibility = Visibility.Visible;
            else if (NavLogs.IsChecked == true) TabLogsView.Visibility = Visibility.Visible;
        }
        #endregion

        #region Login
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtLoginUsername.Text.Trim();
            string password = TxtLoginPassword.Password;
            await _viewModel.LoginAsync(username, password);
        }

        private void TxtLoginPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("ログアウトしますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _viewModel.LogoutAsync();
            }
        }
        #endregion

        #region Quick Actions
        private async void BtnQuickRevert_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.RevertToDefaultAsync();
        }

        private async void BtnRefreshGroups_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshGroupsAsync();
        }

        private async void BtnClearBanner_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearGroupBannerAsync();
        }

        private async void BtnSwitchToThisGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is VRCGroup group)
            {
                await _viewModel.SwitchToGroupAsync(group.GroupId, group.Name);
            }
        }

        private void BtnSetAsDefaultGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is VRCGroup group)
            {
                _viewModel.DefaultGroupId = group.GroupId;
                _viewModel.AddLog($"デフォルトグループを「{group.Name}」に設定しました。", LogLevel.Success);
            }
        }

        private void BtnClearDefaultGroup_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearDefaultGroup();
        }
        #endregion

        #region Rules
        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddRule();
        }

        private void BtnEditRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RuleConfig rule)
            {
                _viewModel.EditRule(rule);
            }
        }

        private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RuleConfig rule)
            {
                _viewModel.DeleteRule(rule);
            }
        }

        private void ChkRuleToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.DataContext is RuleConfig rule)
            {
                _viewModel.ToggleRule(rule);
            }
        }
        #endregion

        #region Logs
        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearLogs();
        }
        #endregion
    }
}