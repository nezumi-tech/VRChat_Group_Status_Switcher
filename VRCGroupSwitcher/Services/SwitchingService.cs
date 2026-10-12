using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRCGroupSwitcher.Models;

namespace VRCGroupSwitcher.Services
{
    public class SwitchingService : IDisposable
    {
        private readonly VRChatApiClient _apiClient;
        private readonly VRChatLogWatcher _logWatcher;
        private readonly SettingsService _settingsService;

        private CancellationTokenSource? _debounceCts;
        private string? _currentRepresentedGroupId;
        private List<VRCGroup> _cachedGroups = new();

        public event Action<string, LogLevel>? ActivityLogged;
        public event Action? StateUpdated;

        public string? CurrentRepresentedGroupId => _currentRepresentedGroupId;
        public VRCGroup? CurrentRepresentedGroup => _cachedGroups.FirstOrDefault(g => g.GroupId == _currentRepresentedGroupId);
        public List<VRCGroup> CachedGroups => _cachedGroups;

        public SwitchingService(
            VRChatApiClient apiClient,
            VRChatLogWatcher logWatcher,
            SettingsService settingsService)
        {
            _apiClient = apiClient;
            _logWatcher = logWatcher;
            _settingsService = settingsService;

            _logWatcher.InstanceChanged += OnInstanceChanged;
            _logWatcher.LeftRoom += OnLeftRoom;
        }

        public async Task RefreshGroupsAsync()
        {
            if (!_apiClient.IsAuthenticated) return;

            try
            {
                var groups = await _apiClient.GetUserGroupsAsync();
                _cachedGroups = groups;

                // Find represented group
                var rep = groups.FirstOrDefault(g => g.IsRepresenting);
                _currentRepresentedGroupId = rep?.GroupId;

                StateUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                ActivityLogged?.Invoke($"グループ情報取得失敗: {ex.Message}", LogLevel.Error);
            }
        }

        private void OnInstanceChanged(VRCInstanceInfo info)
        {
            if (!_settingsService.CurrentSettings.AutoSwitchEnabled)
            {
                return;
            }

            // Debounce to allow world name to settle and avoid spamming during loading
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            int delayMs = Math.Max(1, _settingsService.CurrentSettings.SwitchDelaySeconds) * 1000;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token);
                    if (token.IsCancellationRequested) return;

                    await ProcessSwitchLogicAsync(info);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    ActivityLogged?.Invoke($"自動切り替えエラー: {ex.Message}", LogLevel.Error);
                }
            }, token);
        }

        private void OnLeftRoom()
        {
            if (!_settingsService.CurrentSettings.AutoSwitchEnabled || !_settingsService.CurrentSettings.RevertOnLeave)
            {
                return;
            }

            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000, token);
                    if (token.IsCancellationRequested) return;

                    ActivityLogged?.Invoke("ワールドから退出したため、デフォルト設定に復帰します。", LogLevel.Info);
                    await RevertToDefaultAsync();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    ActivityLogged?.Invoke($"デフォルト復帰エラー: {ex.Message}", LogLevel.Error);
                }
            }, token);
        }

        public bool IsGroupAccessTypeAllowed(string? accessType)
        {
            var settings = _settingsService.CurrentSettings;
            if (string.IsNullOrEmpty(accessType)) return true;

            return accessType switch
            {
                "members" => settings.ReactToGroupOnly,
                "plus" => settings.ReactToGroupPlus,
                "public" => settings.ReactToGroupPublic,
                _ => true
            };
        }

        public async Task ProcessSwitchLogicAsync(VRCInstanceInfo info)
        {
            if (!_apiClient.IsAuthenticated) return;

            if (string.IsNullOrEmpty(info.WorldName) && !string.IsNullOrEmpty(info.WorldId))
            {
                var fetchedName = await _apiClient.GetWorldNameAsync(info.WorldId);
                if (!string.IsNullOrEmpty(fetchedName))
                {
                    info.WorldName = fetchedName;
                }
            }

            var settings = _settingsService.CurrentSettings;
            bool isGroupAccessAllowed = IsGroupAccessTypeAllowed(info.GroupAccessType);

            // 1. Check custom rules
            RuleConfig? matchedRule = null;
            foreach (var rule in settings.CustomRules.Where(r => r.IsEnabled))
            {
                if (rule.TargetType == RuleTargetType.GroupInstance &&
                    !string.IsNullOrEmpty(info.GroupId) &&
                    string.Equals(info.GroupId, rule.TargetValue, StringComparison.OrdinalIgnoreCase))
                {
                    if (!isGroupAccessAllowed)
                    {
                        ActivityLogged?.Invoke($"グループインスタンス（{info.GroupAccessTypeName}）を検知しましたが、設定により対象外のためルール「{rule.Name}」をスキップします。", LogLevel.Info);
                        continue;
                    }
                    matchedRule = rule;
                    break;
                }
                else if (rule.TargetType == RuleTargetType.WorldId &&
                         !string.IsNullOrEmpty(info.WorldId) &&
                         string.Equals(info.WorldId, rule.TargetValue, StringComparison.OrdinalIgnoreCase))
                {
                    matchedRule = rule;
                    break;
                }
                else if (rule.TargetType == RuleTargetType.WorldName &&
                         !string.IsNullOrEmpty(info.WorldName) &&
                         info.WorldName.Contains(rule.TargetValue, StringComparison.OrdinalIgnoreCase))
                {
                    matchedRule = rule;
                    break;
                }
            }

            if (matchedRule != null)
            {
                ActivityLogged?.Invoke($"カスタムルール「{matchedRule.Name}」に一致しました。", LogLevel.Info);
                await ApplyRuleAsync(matchedRule, info);
                return;
            }

            // 2. Check AutoFollowGroupInstance
            if (settings.AutoFollowGroupInstance && !string.IsNullOrEmpty(info.GroupId))
            {
                if (!isGroupAccessAllowed)
                {
                    ActivityLogged?.Invoke($"グループインスタンス（{info.GroupAccessTypeName}）を検知しましたが、設定により自動切り替え対象外です。", LogLevel.Info);
                }
                else
                {
                    var group = _cachedGroups.FirstOrDefault(g => g.GroupId.Equals(info.GroupId, StringComparison.OrdinalIgnoreCase));
                    if (group != null)
                    {
                        ActivityLogged?.Invoke($"所属グループ「{group.Name}」のインスタンス（{info.GroupAccessTypeName}）を検知しました。", LogLevel.Info);
                        await ApplyGroupAndStatusAsync(
                            group.GroupId,
                            group.Name,
                            settings.AutoFollowStatus,
                            ResolveMacros(settings.AutoFollowStatusDescription, group.Name, info.WorldName ?? "ワールド")
                        );
                        return;
                    }
                }
            }

            // 3. Not matched to any rule or group instance -> Revert to Default
            if (settings.RevertOnLeave)
            {
                ActivityLogged?.Invoke("対象外のインスタンスのため、デフォルト設定を適用します。", LogLevel.Info);
                await RevertToDefaultAsync();
            }
        }

        private async Task ApplyRuleAsync(RuleConfig rule, VRCInstanceInfo info)
        {
            string desc = ResolveMacros(rule.TargetStatusDescription, rule.TargetGroupName, info.WorldName ?? "ワールド");
            await ApplyGroupAndStatusAsync(rule.TargetGroupId, rule.TargetGroupName, rule.TargetStatus, desc);
        }

        public async Task RevertToDefaultAsync()
        {
            var settings = _settingsService.CurrentSettings;
            await ApplyGroupAndStatusAsync(
                settings.DefaultGroupId,
                settings.DefaultGroupName ?? "デフォルト",
                settings.DefaultStatus,
                settings.DefaultStatusDescription
            );
        }

        public async Task ApplyGroupAndStatusAsync(string? targetGroupId, string targetGroupName, string? targetStatus, string? targetStatusDesc)
        {
            if (!_apiClient.IsAuthenticated) return;

            // 1. Group banner update
            if (!string.IsNullOrEmpty(targetGroupId))
            {
                if (_currentRepresentedGroupId != targetGroupId)
                {
                    ActivityLogged?.Invoke($"グループバナーを「{targetGroupName}」に変更中...", LogLevel.Info);
                    var (success, msg) = await _apiClient.SetRepresentedGroupAsync(targetGroupId);
                    if (success)
                    {
                        _currentRepresentedGroupId = targetGroupId;
                        foreach (var g in _cachedGroups)
                        {
                            g.IsRepresenting = (g.GroupId == targetGroupId);
                        }
                        ActivityLogged?.Invoke($"グループバナーを「{targetGroupName}」に設定しました！", LogLevel.Success);
                    }
                    else
                    {
                        ActivityLogged?.Invoke($"グループバナー変更失敗: {msg}", LogLevel.Error);
                    }
                }
            }
            else
            {
                // Target is empty/null -> clear banner if currently representing
                string? groupToClear = _currentRepresentedGroupId;
                if (string.IsNullOrEmpty(groupToClear))
                {
                    groupToClear = _cachedGroups.FirstOrDefault(g => g.IsRepresenting)?.GroupId;
                }

                if (!string.IsNullOrEmpty(groupToClear))
                {
                    ActivityLogged?.Invoke("グループバナーを解除中...", LogLevel.Info);
                    var (success, msg) = await _apiClient.ClearRepresentedGroupAsync(groupToClear);
                    if (success)
                    {
                        _currentRepresentedGroupId = null;
                        foreach (var g in _cachedGroups)
                        {
                            g.IsRepresenting = false;
                        }
                        ActivityLogged?.Invoke("グループバナーの掲示を解除しました。", LogLevel.Success);
                    }
                    else
                    {
                        ActivityLogged?.Invoke($"グループバナー解除失敗: {msg}", LogLevel.Error);
                    }
                }
                else
                {
                    ActivityLogged?.Invoke("現在掲示中のグループはありません。", LogLevel.Info);
                }
            }

            // 2. Status update
            if (!string.IsNullOrEmpty(targetStatus) && targetStatus != "unchanged" || !string.IsNullOrEmpty(targetStatusDesc))
            {
                ActivityLogged?.Invoke($"ステータスを更新中... ({targetStatus ?? "維持"}, \"{targetStatusDesc}\")", LogLevel.Info);
                var (success, msg) = await _apiClient.UpdateStatusAsync(targetStatus, targetStatusDesc);
                if (success)
                {
                    ActivityLogged?.Invoke($"ステータスを更新しました: [{targetStatus}] {targetStatusDesc}", LogLevel.Success);
                }
                else
                {
                    ActivityLogged?.Invoke($"ステータス更新失敗: {msg}", LogLevel.Error);
                }
            }

            StateUpdated?.Invoke();
        }

        private static string ResolveMacros(string template, string groupName, string worldName)
        {
            if (string.IsNullOrEmpty(template)) return "";
            return template
                .Replace("{group_name}", groupName)
                .Replace("{world_name}", worldName);
        }

        public void Dispose()
        {
            _debounceCts?.Dispose();
            _logWatcher.InstanceChanged -= OnInstanceChanged;
            _logWatcher.LeftRoom -= OnLeftRoom;
        }
    }
}
