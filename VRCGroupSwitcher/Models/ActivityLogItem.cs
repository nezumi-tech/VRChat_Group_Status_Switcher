using System;

namespace VRCGroupSwitcher.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ActivityLogItem
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; } = LogLevel.Info;

        public string TimeString => Timestamp.ToString("HH:mm:ss");

        public string ColorHex => Level switch
        {
            LogLevel.Success => "#10B981", // Emerald green
            LogLevel.Warning => "#F59E0B", // Amber
            LogLevel.Error => "#EF4444",   // Red
            _ => "#94A3B8"                 // Slate
        };
    }
}
