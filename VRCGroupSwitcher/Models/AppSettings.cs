using System;
using System.Collections.Generic;

namespace VRCGroupSwitcher.Models
{
    public class AppSettings
    {
        // Default group to show when outside any matched instance
        public string? DefaultGroupId { get; set; } = null;
        public string? DefaultGroupName { get; set; } = null;

        // Default status ("unchanged", "active", "join me", "ask me", "busy")
        public string DefaultStatus { get; set; } = "active";
        public string DefaultStatusDescription { get; set; } = "";

        // Toggle master auto-switching
        public bool AutoSwitchEnabled { get; set; } = true;

        // Automatically follow any group instance (~group(grp_...)) if user belongs to the group
        public bool AutoFollowGroupInstance { get; set; } = true;

        // When in auto-follow mode, what status to set? ("unchanged", "active", "join me", "ask me", "busy")
        public string AutoFollowStatus { get; set; } = "unchanged";
        public string AutoFollowStatusDescription { get; set; } = "{group_name} イベント参加中";

        // Group instance types to react to (Group Only, Group+, Group Public)
        public bool ReactToGroupOnly { get; set; } = true;
        public bool ReactToGroupPlus { get; set; } = true;
        public bool ReactToGroupPublic { get; set; } = true;

        // Revert to default when leaving group instance / world
        public bool RevertOnLeave { get; set; } = true;

        // Switch delay in seconds to ensure stable room connection
        public int SwitchDelaySeconds { get; set; } = 3;

        // Custom VRChat log directory path (null for default %LOCALAPPDATA%Low\VRChat\VRChat)
        public string? CustomLogDirectory { get; set; } = null;

        // Minimize to notification area (system tray)
        public bool MinimizeToTray { get; set; } = true;

        // Close button (X) minimizes to notification area instead of exiting
        public bool CloseToTray { get; set; } = true;

        // Launch automatically on Windows startup
        public bool StartWithWindows { get; set; } = false;

        // Notification sound / toast
        public bool ShowNotifications { get; set; } = true;

        // List of custom rules
        public List<RuleConfig> CustomRules { get; set; } = new();
    }
}
