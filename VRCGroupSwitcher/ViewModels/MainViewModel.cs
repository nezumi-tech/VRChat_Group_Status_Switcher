using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VRCGroupSwitcher.Models;
using VRCGroupSwitcher.Services;
using VRCGroupSwitcher.Views;

namespace VRCGroupSwitcher.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly VRChatApiClient _apiClient;
        private readonly VRChatLogWatcher _logWatcher;
        private readonly SettingsService _settingsService;
        private readonly SwitchingService _switchingService;
        private readonly UpdateCheckerService _updateCheckerService;
        private readonly Dispatcher _dispatcher;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Version info (using InformationalVersion)
        public string AppVersion { get; } = UpdateCheckerService.GetCurrentInformationalVersion();
        public string WindowTitle => $"VRC Group Switcher v{AppVersion}";

        // Update Notification Properties
        private bool _hasUpdateNotification;
        public bool HasUpdateNotification
        {
            get => _hasUpdateNotification;
            set => SetField(ref _hasUpdateNotification, value);
        }

        private string _latestVersion = "";
        public string LatestVersion
        {
            get => _latestVersion;
            set => SetField(ref _latestVersion, value);
        }

        private string _updateReleaseTitle = "";
        public string UpdateReleaseTitle
        {
            get => _updateReleaseTitle;
            set => SetField(ref _updateReleaseTitle, value);
        }

        private string _boothDownloadUrl = UpdateCheckerService.DefaultBoothUrl;
        public string BoothDownloadUrl
        {
            get => _boothDownloadUrl;
            set => SetField(ref _boothDownloadUrl, value);
        }

        private bool _isCheckingUpdate;
        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            set => SetField(ref _isCheckingUpdate, value);
        }

        private string _updateCheckStatusText = "確認待機中";
        public string UpdateCheckStatusText
        {
            get => _updateCheckStatusText;
            set => SetField(ref _updateCheckStatusText, value);
        }

        // Observable collections for UI
        public ObservableCollection<VRCGroup> Groups { get; } = new();
        public ObservableCollection<RuleConfig> CustomRules { get; } = new();
        public ObservableCollection<ActivityLogItem> ActivityLogs { get; } = new();

        #region Properties
        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetField(ref _isLoggedIn, value);
        }

        private bool _isLoggingIn;
        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => SetField(ref _isLoggingIn, value);
        }

        private string? _loginErrorMessage;
        public string? LoginErrorMessage
        {
            get => _loginErrorMessage;
            set => SetField(ref _loginErrorMessage, value);
        }

        private string _loginStatusText = "ログイン処理中...";
        public string LoginStatusText
        {
            get => _loginStatusText;
            set => SetField(ref _loginStatusText, value);
        }

        private string _loginUsername = "";
        public string LoginUsername
        {
            get => _loginUsername;
            set => SetField(ref _loginUsername, value);
        }

        private VRCUser? _currentUser;
        public VRCUser? CurrentUser
        {
            get => _currentUser;
            set
            {
                if (SetField(ref _currentUser, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(AvatarUrl));
                    OnPropertyChanged(nameof(UserStatus));
                    OnPropertyChanged(nameof(UserStatusDescription));
                }
            }
        }

        public string DisplayName => CurrentUser?.DisplayName ?? "未ログイン";
        public string AvatarUrl => CurrentUser?.AvatarDisplayUrl ?? "";
        public string UserStatus => CurrentUser?.Status ?? "offline";
        public string UserStatusDescription => CurrentUser?.StatusDescription ?? "";

        // Active VRChat Instance info
        private string _currentWorldName = "待機中 (ワールド未検知)";
        public string CurrentWorldName
        {
            get => _currentWorldName;
            set => SetField(ref _currentWorldName, value);
        }

        private string _currentWorldId = "";
        public string CurrentWorldId
        {
            get => _currentWorldId;
            set => SetField(ref _currentWorldId, value);
        }

        private string _detectedGroupId = "";
        public string DetectedGroupId
        {
            get => _detectedGroupId;
            set => SetField(ref _detectedGroupId, value);
        }

        private string _currentInstanceType = "";
        public string CurrentInstanceType
        {
            get => _currentInstanceType;
            set => SetField(ref _currentInstanceType, value);
        }

        private bool _isVRChatActive;
        public bool IsVRChatActive
        {
            get => _isVRChatActive;
            set => SetField(ref _isVRChatActive, value);
        }

        // Currently represented group
        private VRCGroup? _currentRepresentedGroup;
        public VRCGroup? CurrentRepresentedGroup
        {
            get => _currentRepresentedGroup;
            set => SetField(ref _currentRepresentedGroup, value);
        }

        // Settings Proxies
        public bool AutoSwitchEnabled
        {
            get => _settingsService.CurrentSettings.AutoSwitchEnabled;
            set
            {
                if (_settingsService.CurrentSettings.AutoSwitchEnabled != value)
                {
                    _settingsService.CurrentSettings.AutoSwitchEnabled = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                    AddLog(value ? "自動切り替えを【有効】にしました。" : "自動切り替えを【無効】にしました。", LogLevel.Info);
                }
            }
        }

        public bool AutoFollowGroupInstance
        {
            get => _settingsService.CurrentSettings.AutoFollowGroupInstance;
            set
            {
                if (_settingsService.CurrentSettings.AutoFollowGroupInstance != value)
                {
                    _settingsService.CurrentSettings.AutoFollowGroupInstance = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool RevertOnLeave
        {
            get => _settingsService.CurrentSettings.RevertOnLeave;
            set
            {
                if (_settingsService.CurrentSettings.RevertOnLeave != value)
                {
                    _settingsService.CurrentSettings.RevertOnLeave = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public int SwitchDelaySeconds
        {
            get => _settingsService.CurrentSettings.SwitchDelaySeconds;
            set
            {
                if (_settingsService.CurrentSettings.SwitchDelaySeconds != value)
                {
                    _settingsService.CurrentSettings.SwitchDelaySeconds = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public string? DefaultGroupId
        {
            get => _settingsService.CurrentSettings.DefaultGroupId;
            set
            {
                // WPF ComboBox の自動リセットによる null や空文字は絶対に設定として保存しない
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                if (_settingsService.CurrentSettings.DefaultGroupId != value)
                {
                    _settingsService.CurrentSettings.DefaultGroupId = value;
                    var grp = Groups.FirstOrDefault(g => g.GroupId == value);
                    _settingsService.CurrentSettings.DefaultGroupName = grp?.Name ?? "(バナーなし)";
                    _settingsService.Save();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DefaultGroupName));
                }
            }
        }

        public void ClearDefaultGroup()
        {
            _settingsService.CurrentSettings.DefaultGroupId = null;
            _settingsService.CurrentSettings.DefaultGroupName = "(バナーなし)";
            _settingsService.Save();
            OnPropertyChanged(nameof(DefaultGroupId));
            OnPropertyChanged(nameof(DefaultGroupName));
            AddLog("デフォルト掲示グループを解除しました（バナーなし）。", LogLevel.Info);
        }

        public string DefaultGroupName => _settingsService.CurrentSettings.DefaultGroupName ?? "(バナーなし)";

        public string DefaultStatus
        {
            get => _settingsService.CurrentSettings.DefaultStatus;
            set
            {
                if (_settingsService.CurrentSettings.DefaultStatus != value)
                {
                    _settingsService.CurrentSettings.DefaultStatus = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public string DefaultStatusDescription
        {
            get => _settingsService.CurrentSettings.DefaultStatusDescription;
            set
            {
                if (_settingsService.CurrentSettings.DefaultStatusDescription != value)
                {
                    _settingsService.CurrentSettings.DefaultStatusDescription = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public string AutoFollowStatus
        {
            get => _settingsService.CurrentSettings.AutoFollowStatus;
            set
            {
                if (_settingsService.CurrentSettings.AutoFollowStatus != value)
                {
                    _settingsService.CurrentSettings.AutoFollowStatus = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public string AutoFollowStatusDescription
        {
            get => _settingsService.CurrentSettings.AutoFollowStatusDescription;
            set
            {
                if (_settingsService.CurrentSettings.AutoFollowStatusDescription != value)
                {
                    _settingsService.CurrentSettings.AutoFollowStatusDescription = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool MinimizeToTray
        {
            get => _settingsService.CurrentSettings.MinimizeToTray;
            set
            {
                if (_settingsService.CurrentSettings.MinimizeToTray != value)
                {
                    _settingsService.CurrentSettings.MinimizeToTray = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool CloseToTray
        {
            get => _settingsService.CurrentSettings.CloseToTray;
            set
            {
                if (_settingsService.CurrentSettings.CloseToTray != value)
                {
                    _settingsService.CurrentSettings.CloseToTray = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool StartWithWindows
        {
            get => _settingsService.CurrentSettings.StartWithWindows;
            set
            {
                if (_settingsService.CurrentSettings.StartWithWindows != value)
                {
                    _settingsService.CurrentSettings.StartWithWindows = value;
                    StartupService.SetStartup(value);
                    _settingsService.Save();
                    OnPropertyChanged();
                    AddLog(value ? "Windowsスタートアップ自動起動を【有効】に設定しました。" : "Windowsスタートアップ自動起動を【無効】に設定しました。", LogLevel.Info);
                }
            }
        }

        public bool ReactToGroupOnly
        {
            get => _settingsService.CurrentSettings.ReactToGroupOnly;
            set
            {
                if (_settingsService.CurrentSettings.ReactToGroupOnly != value)
                {
                    _settingsService.CurrentSettings.ReactToGroupOnly = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                    AddLog(value ? "グループオンリーへの反応を【有効】に設定しました。" : "グループオンリーへの反応を【無効】に設定しました。", LogLevel.Info);
                }
            }
        }

        public bool ReactToGroupPlus
        {
            get => _settingsService.CurrentSettings.ReactToGroupPlus;
            set
            {
                if (_settingsService.CurrentSettings.ReactToGroupPlus != value)
                {
                    _settingsService.CurrentSettings.ReactToGroupPlus = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                    AddLog(value ? "グループプラスへの反応を【有効】に設定しました。" : "グループプラスへの反応を【無効】に設定しました。", LogLevel.Info);
                }
            }
        }

        public bool ReactToGroupPublic
        {
            get => _settingsService.CurrentSettings.ReactToGroupPublic;
            set
            {
                if (_settingsService.CurrentSettings.ReactToGroupPublic != value)
                {
                    _settingsService.CurrentSettings.ReactToGroupPublic = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                    AddLog(value ? "グループパブリックへの反応を【有効】に設定しました。" : "グループパブリックへの反応を【無効】に設定しました。", LogLevel.Info);
                }
            }
        }
        #endregion

        public MainViewModel(
            VRChatApiClient apiClient,
            VRChatLogWatcher logWatcher,
            SettingsService settingsService,
            SwitchingService switchingService,
            UpdateCheckerService? updateCheckerService = null)
        {
            _apiClient = apiClient;
            _logWatcher = logWatcher;
            _settingsService = settingsService;
            _switchingService = switchingService;
            _updateCheckerService = updateCheckerService ?? new UpdateCheckerService();
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            // Sync startup registry status if needed
            bool isRegEnabled = StartupService.IsStartupEnabled();
            if (_settingsService.CurrentSettings.StartWithWindows != isRegEnabled)
            {
                _settingsService.CurrentSettings.StartWithWindows = isRegEnabled;
                _settingsService.Save();
            }

            // Hook services
            CredentialStorage.StorageLog += AddLog;
            _settingsService.SettingsLog += AddLog;
            _apiClient.ApiLog += AddLog;
            _logWatcher.InstanceChanged += OnInstanceChanged;
            _logWatcher.LeftRoom += OnLeftRoom;
            _switchingService.ActivityLogged += AddLog;
            _switchingService.StateUpdated += OnStateUpdated;

            // Load saved rules
            foreach (var r in _settingsService.CurrentSettings.CustomRules)
            {
                CustomRules.Add(r);
            }

            AddLog("アプリケーションを起動しました。", LogLevel.Info);
            if (!string.IsNullOrEmpty(_settingsService.ActiveSettingsFilePath))
            {
                AddLog($"設定ファイルを読み込みました ({_settingsService.ActiveSettingsFilePath})", LogLevel.Info);
            }
        }

        public async Task InitializeAsync()
        {
            IsLoggingIn = true;
            LoginStatusText = "保存されたセッションを確認中...";
            AddLog("保存されたセッションを確認中...", LogLevel.Info);

            var stored = CredentialStorage.LoadCredentials();
            if (stored != null && !string.IsNullOrEmpty(stored.SavedUsername))
            {
                LoginUsername = stored.SavedUsername;
            }

            bool restored = await _apiClient.TryRestoreSessionAsync();
            if (restored && _apiClient.CurrentUser != null)
            {
                CurrentUser = _apiClient.CurrentUser;
                IsLoggedIn = true;
                AddLog($"ようこそ、{CurrentUser.DisplayName} さん！", LogLevel.Success);
                await RefreshGroupsAsync();
            }
            else
            {
                AddLog("ログインが必要です。VRChatアカウント情報を入力してください。", LogLevel.Info);
            }

            IsLoggingIn = false;
        }

        public async Task LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                LoginErrorMessage = "ユーザー名とパスワードを入力してください。";
                return;
            }

            IsLoggingIn = true;
            LoginStatusText = "VRChatへログイン試行中...";
            LoginErrorMessage = null;
            AddLog("VRChatへログイン試行中...", LogLevel.Info);

            var result = await _apiClient.LoginAsync(username, password);

            if (result.RequiresTwoFactor)
            {
                IsLoggingIn = false;
                string prompt = result.TwoFactorType == "emailOtp"
                    ? "メール宛に送信された認証コードを入力してください。"
                    : "認証アプリ(TOTP)に表示されている6桁コードを入力してください。";

                var dialog = new TwoFactorDialog(prompt);
                if (dialog.ShowDialog() == true)
                {
                    IsLoggingIn = true;
                    AddLog("2段階認証コードを検証中...", LogLevel.Info);
                    result = await _apiClient.VerifyTwoFactorAsync(dialog.Code, result.TwoFactorType ?? "totp");
                }
                else
                {
                    AddLog("2段階認証をキャンセルしました。", LogLevel.Warning);
                    return;
                }
            }

            if (result.Success && result.User != null)
            {
                CurrentUser = result.User;
                IsLoggedIn = true;
                LoginErrorMessage = null;
                AddLog($"ログイン成功: {CurrentUser.DisplayName}", LogLevel.Success);
                await RefreshGroupsAsync();
            }
            else
            {
                LoginErrorMessage = result.ErrorMessage ?? "ログインに失敗しました。";
                AddLog(LoginErrorMessage, LogLevel.Error);
            }

            IsLoggingIn = false;
        }

        public async Task LogoutAsync()
        {
            await _apiClient.LogoutAsync();
            CurrentUser = null;
            IsLoggedIn = false;
            Groups.Clear();
            CurrentRepresentedGroup = null;
            AddLog("ログアウトしました。", LogLevel.Info);
        }

        public async Task RefreshGroupsAsync()
        {
            if (!IsLoggedIn) return;

            AddLog("所属グループ情報を取得中...", LogLevel.Info);
            await _switchingService.RefreshGroupsAsync();

            _dispatcher.Invoke(() =>
            {
                string? savedDefaultId = _settingsService.CurrentSettings.DefaultGroupId;
                var newGroups = _switchingService.CachedGroups;

                // 差分同期（コレクション全クリアを避けることでComboBoxの選択解除やチラつきを完全に防止）
                for (int i = Groups.Count - 1; i >= 0; i--)
                {
                    if (!newGroups.Any(g => g.GroupId == Groups[i].GroupId))
                    {
                        Groups.RemoveAt(i);
                    }
                }

                for (int i = 0; i < newGroups.Count; i++)
                {
                    var ng = newGroups[i];
                    var existing = Groups.FirstOrDefault(g => g.GroupId == ng.GroupId);
                    if (existing != null)
                    {
                        existing.Name = ng.Name;
                        existing.ShortCode = ng.ShortCode;
                        existing.Discriminator = ng.Discriminator;
                        existing.Description = ng.Description;
                        existing.IconUrl = ng.IconUrl;
                        existing.BannerUrl = ng.BannerUrl;
                        existing.MemberCount = ng.MemberCount;
                        existing.IsRepresenting = ng.IsRepresenting;
                    }
                    else
                    {
                        Groups.Insert(Math.Min(i, Groups.Count), ng);
                    }
                }

                if (!string.IsNullOrEmpty(savedDefaultId))
                {
                    var grp = Groups.FirstOrDefault(g => g.GroupId == savedDefaultId);
                    if (grp != null)
                    {
                        _settingsService.CurrentSettings.DefaultGroupName = grp.Name;
                    }
                }

                CurrentRepresentedGroup = _switchingService.CurrentRepresentedGroup;
                OnPropertyChanged(nameof(CurrentRepresentedGroup));
                OnPropertyChanged(nameof(DefaultGroupId));
                OnPropertyChanged(nameof(DefaultGroupName));
            });

            AddLog($"{Groups.Count} 件の所属グループを読み込みました。", LogLevel.Success);
        }

        public async Task SwitchToGroupAsync(string groupId, string groupName)
        {
            AddLog($"手動切り替え: 「{groupName}」を掲示します。", LogLevel.Info);
            await _switchingService.ApplyGroupAndStatusAsync(groupId, groupName, "unchanged", null);
        }

        public async Task RevertToDefaultAsync()
        {
            AddLog("手動操作: デフォルト設定に復帰します。", LogLevel.Info);
            await _switchingService.RevertToDefaultAsync();
        }

        public async Task ClearGroupBannerAsync()
        {
            AddLog("手動操作: グループバナーを解除します。", LogLevel.Info);
            await _switchingService.ApplyGroupAndStatusAsync(null, "解除", "unchanged", null);
        }

        public void AddRule()
        {
            var dialog = new RuleEditDialog(null, Groups.ToList());
            if (dialog.ShowDialog() == true)
            {
                CustomRules.Add(dialog.Rule);
                _settingsService.CurrentSettings.CustomRules = CustomRules.ToList();
                _settingsService.Save();
                AddLog($"ルール「{dialog.Rule.Name}」を追加しました。", LogLevel.Success);
            }
        }

        public void EditRule(RuleConfig rule)
        {
            var dialog = new RuleEditDialog(rule, Groups.ToList());
            if (dialog.ShowDialog() == true)
            {
                int index = CustomRules.IndexOf(rule);
                if (index >= 0)
                {
                    CustomRules[index] = dialog.Rule;
                }
                _settingsService.CurrentSettings.CustomRules = CustomRules.ToList();
                _settingsService.Save();
                AddLog($"ルール「{dialog.Rule.Name}」を更新しました。", LogLevel.Success);
            }
        }

        public void DeleteRule(RuleConfig rule)
        {
            if (MessageBox.Show($"ルール「{rule.Name}」を削除しますか？", "ルールの削除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                CustomRules.Remove(rule);
                _settingsService.CurrentSettings.CustomRules = CustomRules.ToList();
                _settingsService.Save();
                AddLog($"ルール「{rule.Name}」を削除しました。", LogLevel.Info);
            }
        }

        public void ToggleRule(RuleConfig rule)
        {
            rule.IsEnabled = !rule.IsEnabled;
            _settingsService.CurrentSettings.CustomRules = CustomRules.ToList();
            _settingsService.Save();
            OnPropertyChanged(nameof(CustomRules));
        }

        public void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            _dispatcher.Invoke(() =>
            {
                var item = new ActivityLogItem
                {
                    Timestamp = DateTime.Now,
                    Message = message,
                    Level = level
                };

                ActivityLogs.Insert(0, item);
                if (ActivityLogs.Count > 100)
                {
                    ActivityLogs.RemoveAt(ActivityLogs.Count - 1);
                }
            });
        }

        public void ClearLogs()
        {
            ActivityLogs.Clear();
        }

        public async Task<bool> CheckForUpdatesAsync(bool isManual = false)
        {
            if (IsCheckingUpdate) return false;
            IsCheckingUpdate = true;
            UpdateCheckStatusText = "更新を確認中...";

            try
            {
                var info = await _updateCheckerService.CheckForUpdatesAsync();
                if (!string.IsNullOrEmpty(info.ErrorMessage))
                {
                    UpdateCheckStatusText = "確認失敗";
                    if (isManual)
                    {
                        AddLog($"アップデート確認に失敗しました: {info.ErrorMessage}", LogLevel.Warning);
                    }
                    return false;
                }

                LatestVersion = info.LatestVersion;
                UpdateReleaseTitle = info.ReleaseTitle;
                BoothDownloadUrl = info.BoothDownloadUrl;

                if (info.IsUpdateAvailable)
                {
                    HasUpdateNotification = true;
                    UpdateCheckStatusText = $"v{info.LatestVersion} に更新可能";
                    AddLog($"新しいバージョン (v{info.LatestVersion}) が利用可能です！BOOTHよりダウンロードしてください。", LogLevel.Warning);
                    return true;
                }
                else
                {
                    HasUpdateNotification = false;
                    UpdateCheckStatusText = "最新版です";
                    if (isManual)
                    {
                        AddLog($"現在のバージョン (v{AppVersion}) は最新です。", LogLevel.Success);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateCheckStatusText = "確認エラー";
                if (isManual)
                {
                    AddLog($"アップデート確認エラー: {ex.Message}", LogLevel.Error);
                }
                return false;
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        public void OpenBoothPage()
        {
            UpdateCheckerService.OpenBoothDownloadPage(BoothDownloadUrl);
        }

        public void DismissUpdateNotification()
        {
            HasUpdateNotification = false;
        }

        private void OnInstanceChanged(VRCInstanceInfo info)
        {
            _dispatcher.Invoke(() =>
            {
                IsVRChatActive = true;
                CurrentWorldName = !string.IsNullOrEmpty(info.WorldName) ? info.WorldName : "入室中...";
                CurrentWorldId = info.WorldId;
                DetectedGroupId = info.GroupId ?? "(通常インスタンス)";
                CurrentInstanceType = !string.IsNullOrEmpty(info.GroupId) ? info.GroupAccessTypeName : "";
            });

            if (string.IsNullOrEmpty(info.WorldName) && !string.IsNullOrEmpty(info.WorldId) && IsLoggedIn)
            {
                _ = Task.Run(async () =>
                {
                    var name = await _apiClient.GetWorldNameAsync(info.WorldId);
                    if (!string.IsNullOrEmpty(name))
                    {
                        info.WorldName = name;
                        _dispatcher.Invoke(() =>
                        {
                            if (CurrentWorldId == info.WorldId && (CurrentWorldName == "入室中..." || string.IsNullOrEmpty(CurrentWorldName)))
                            {
                                CurrentWorldName = name;
                            }
                        });
                    }
                });
            }
        }

        private void OnLeftRoom()
        {
            _dispatcher.Invoke(() =>
            {
                CurrentWorldName = "待機中 (ワールド外)";
                CurrentWorldId = "";
                DetectedGroupId = "";
                CurrentInstanceType = "";
            });
        }

        private void OnStateUpdated()
        {
            _dispatcher.Invoke(() =>
            {
                CurrentRepresentedGroup = _switchingService.CurrentRepresentedGroup;
                if (_apiClient.CurrentUser != null)
                {
                    CurrentUser = _apiClient.CurrentUser;
                }
                OnPropertyChanged(nameof(CurrentRepresentedGroup));
                OnPropertyChanged(nameof(UserStatus));
                OnPropertyChanged(nameof(UserStatusDescription));
            });
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static MainViewModel CreateMockViewModel(string? mockBannerPath = null, string? mockAvatarPath = null)
        {
            var apiClient = new VRChatApiClient();
            var logWatcher = new VRChatLogWatcher();
            var settingsService = new SettingsService();
            settingsService.CurrentSettings.DefaultGroupName = "Default group [DFLT]";
            settingsService.CurrentSettings.DefaultStatus = "active";

            var switchingService = new SwitchingService(apiClient, logWatcher, settingsService);
            var updateChecker = new UpdateCheckerService();

            var vm = new MainViewModel(apiClient, logWatcher, settingsService, switchingService, updateChecker)
            {
                IsLoggedIn = true,
                IsVRChatActive = true,
                CurrentWorldName = "テストワールド",
                CurrentWorldId = "wrld_test_00000000",
                CurrentInstanceType = "Group+",
                DetectedGroupId = "grp_test_0000",
                AutoSwitchEnabled = true,
                HasUpdateNotification = false
            };

            vm.CurrentUser = new VRCUser
            {
                Id = "usr_test_00000",
                DisplayName = "Test user / テストユーザー",
                Username = "test_user",
                Status = "join me",
                StatusDescription = "テストイベント参加中 🌟 | Auto switched",
                CurrentAvatarThumbnailImageUrl = mockAvatarPath ?? ""
            };

            var mockGroup = new VRCGroup
            {
                Id = "grp_test_0000",
                Name = "Test group [TEST]",
                ShortCode = "TEST",
                Discriminator = "0000",
                BannerUrl = mockBannerPath ?? ""
            };
            vm.CurrentRepresentedGroup = mockGroup;
            vm.Groups.Add(mockGroup);
            vm.Groups.Add(new VRCGroup { Id = "grp_test_0001", Name = "Default group [DFLT]", ShortCode = "DFLT", Discriminator = "0000" });
            vm.Groups.Add(new VRCGroup { Id = "grp_test_0002", Name = "Sub group 2 [SUB2]", ShortCode = "SUB2", Discriminator = "0000" });

            vm.CustomRules.Clear();
            vm.CustomRules.Add(new RuleConfig
            {
                Id = "rule_test_1",
                Name = "テスト集会イベント",
                TargetType = RuleTargetType.GroupInstance,
                TargetValue = "grp_test_0000",
                TargetGroupId = "grp_test_0000",
                TargetGroupName = "Test group [TEST]",
                TargetStatus = "unchanged",
                TargetStatusDescription = "テストイベント参加中 🌟",
                IsEnabled = true
            });
            vm.CustomRules.Add(new RuleConfig
            {
                Id = "rule_test_2",
                Name = "テスト会場ワールド",
                TargetType = RuleTargetType.WorldId,
                TargetValue = "wrld_test_event",
                TargetGroupId = "grp_test_0001",
                TargetGroupName = "Default group [DFLT]",
                TargetStatus = "ask me",
                TargetStatusDescription = "テスト集会中 🍸",
                IsEnabled = true
            });
            vm.CustomRules.Add(new RuleConfig
            {
                Id = "rule_test_3",
                Name = "テスト用作業部屋",
                TargetType = RuleTargetType.WorldName,
                TargetValue = "Test Study Room",
                TargetGroupId = "",
                TargetGroupName = "(バナー掲示なし)",
                TargetStatus = "busy",
                TargetStatusDescription = "テスト作業集中モード",
                IsEnabled = true
            });

            vm.ActivityLogs.Clear();
            vm.ActivityLogs.Add(new ActivityLogItem { Timestamp = DateTime.Now.AddMinutes(-5), Level = LogLevel.Info, Message = "アプリケーションを起動しました。" });
            vm.ActivityLogs.Add(new ActivityLogItem { Timestamp = DateTime.Now.AddMinutes(-4), Level = LogLevel.Success, Message = "ログインに成功しました (Test user / テストユーザー)" });
            vm.ActivityLogs.Add(new ActivityLogItem { Timestamp = DateTime.Now.AddMinutes(-2), Level = LogLevel.Info, Message = "ワールド「テストワールド」に入場しました。" });
            vm.ActivityLogs.Add(new ActivityLogItem { Timestamp = DateTime.Now.AddMinutes(-2), Level = LogLevel.Success, Message = "グループインスタンス(Group+)を検知: 「Test group」を自動掲示しました。" });
            vm.ActivityLogs.Add(new ActivityLogItem { Timestamp = DateTime.Now.AddMinutes(-2), Level = LogLevel.Success, Message = "ステータスメッセージを「テストイベント参加中 🌟」に更新しました。" });

            return vm;
        }
    }
}
