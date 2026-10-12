using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VRCGroupSwitcher.Models;

namespace VRCGroupSwitcher.Views
{
    public partial class RuleEditDialog : Window
    {
        public RuleConfig Rule { get; private set; }
        private readonly List<VRCGroup> _groups;

        public RuleEditDialog(RuleConfig? existingRule, List<VRCGroup> availableGroups)
        {
            InitializeComponent();
            _groups = availableGroups;
            Rule = existingRule != null ? CloneRule(existingRule) : new RuleConfig();

            // Populate groups combobox
            var groupList = new List<VRCGroup>
            {
                new VRCGroup { Id = "", Name = "(バナー掲示なし)" }
            };
            groupList.AddRange(availableGroups);
            CmbTargetGroup.ItemsSource = groupList;

            // Populate condition group combobox (for GroupInstance condition)
            var condGroupList = new List<VRCGroup>
            {
                new VRCGroup { Id = "", Name = "(所属グループから選択...)" }
            };
            condGroupList.AddRange(availableGroups);
            CmbConditionGroup.ItemsSource = condGroupList;

            LoadRuleData();
        }

        private void LoadRuleData()
        {
            TxtName.Text = Rule.Name;
            TxtTargetValue.Text = Rule.TargetValue;
            TxtStatusDesc.Text = Rule.TargetStatusDescription;
            ChkEnabled.IsChecked = Rule.IsEnabled;

            // Target Type
            foreach (ComboBoxItem item in CmbTargetType.Items)
            {
                if (item.Tag?.ToString() == Rule.TargetType.ToString())
                {
                    CmbTargetType.SelectedItem = item;
                    break;
                }
            }

            // Condition Group (if group instance)
            if (Rule.TargetType == RuleTargetType.GroupInstance && !string.IsNullOrEmpty(Rule.TargetValue))
            {
                CmbConditionGroup.SelectedValue = Rule.TargetValue;
            }

            // Target Group
            CmbTargetGroup.SelectedValue = Rule.TargetGroupId ?? "";

            // Target Status
            foreach (ComboBoxItem item in CmbTargetStatus.Items)
            {
                if (item.Tag?.ToString() == Rule.TargetStatus)
                {
                    CmbTargetStatus.SelectedItem = item;
                    break;
                }
            }

            UpdateConditionVisibility();
        }

        private void CmbTargetType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConditionVisibility();
        }

        private void UpdateConditionVisibility()
        {
            if (LblTargetValue == null || CmbConditionGroup == null) return;

            var selected = (CmbTargetType?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (selected == "GroupInstance")
            {
                LblTargetValue.Text = "条件値 (所属グループ選択 または グループID grp_...)";
                CmbConditionGroup.Visibility = Visibility.Visible;
                if (TxtTargetValuePlaceholder != null) TxtTargetValuePlaceholder.Text = "grp_...";
            }
            else if (selected == "WorldId")
            {
                LblTargetValue.Text = "条件値 (ワールドID wrld_...)";
                CmbConditionGroup.Visibility = Visibility.Collapsed;
                if (TxtTargetValuePlaceholder != null) TxtTargetValuePlaceholder.Text = "wrld_...";
            }
            else if (selected == "WorldName")
            {
                LblTargetValue.Text = "条件値 (ワールド名 部分一致)";
                CmbConditionGroup.Visibility = Visibility.Collapsed;
                if (TxtTargetValuePlaceholder != null) TxtTargetValuePlaceholder.Text = "ワールド名 (部分一致)...";
            }
            else
            {
                LblTargetValue.Text = "条件値";
                CmbConditionGroup.Visibility = Visibility.Collapsed;
                if (TxtTargetValuePlaceholder != null) TxtTargetValuePlaceholder.Text = "";
            }
        }

        private void CmbConditionGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbConditionGroup.SelectedItem is VRCGroup selectedGroup && !string.IsNullOrEmpty(selectedGroup.GroupId))
            {
                TxtTargetValue.Text = selectedGroup.GroupId;
                if (string.IsNullOrWhiteSpace(TxtName.Text) || TxtName.Text == "イベント集会所ルール")
                {
                    TxtName.Text = $"{selectedGroup.Name} ルール";
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            string targetValue = TxtTargetValue.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("ルール名を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(targetValue))
            {
                MessageBox.Show("条件値を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Rule.Name = name;
            Rule.TargetValue = targetValue;
            Rule.IsEnabled = ChkEnabled.IsChecked ?? true;

            var typeTag = (CmbTargetType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (Enum.TryParse<RuleTargetType>(typeTag, out var ruleType))
            {
                Rule.TargetType = ruleType;
            }

            var selectedGroup = CmbTargetGroup.SelectedItem as VRCGroup;
            Rule.TargetGroupId = selectedGroup?.GroupId ?? "";
            Rule.TargetGroupName = selectedGroup?.Name ?? "(バナーなし)";

            var statusTag = (CmbTargetStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            Rule.TargetStatus = statusTag ?? "unchanged";
            Rule.TargetStatusDescription = TxtStatusDesc.Text;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static RuleConfig CloneRule(RuleConfig r)
        {
            return new RuleConfig
            {
                Id = r.Id,
                Name = r.Name,
                TargetType = r.TargetType,
                TargetValue = r.TargetValue,
                TargetGroupId = r.TargetGroupId,
                TargetGroupName = r.TargetGroupName,
                TargetStatus = r.TargetStatus,
                TargetStatusDescription = r.TargetStatusDescription,
                IsEnabled = r.IsEnabled
            };
        }
    }
}
