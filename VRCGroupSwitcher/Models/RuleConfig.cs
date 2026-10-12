using System;

namespace VRCGroupSwitcher.Models
{
    public enum RuleTargetType
    {
        GroupInstance, // Match by Group ID from group instance (~group(grp_...))
        WorldId,       // Match by World ID (wrld_...)
        WorldName      // Match by World Name (contains or exact)
    }

    public class RuleConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "新規ルール";
        public RuleTargetType TargetType { get; set; } = RuleTargetType.GroupInstance;
        
        // Target match value: e.g. grp_xxx, wrld_xxx, or "ポピー横丁"
        public string TargetValue { get; set; } = string.Empty;

        // Group to set when entering
        public string TargetGroupId { get; set; } = string.Empty;
        public string TargetGroupName { get; set; } = string.Empty;

        // Status to set: "unchanged", "active", "join me", "ask me", "busy"
        public string TargetStatus { get; set; } = "unchanged";

        // Status description: e.g. "{group_name} イベント中", or custom text
        public string TargetStatusDescription { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
    }
}
