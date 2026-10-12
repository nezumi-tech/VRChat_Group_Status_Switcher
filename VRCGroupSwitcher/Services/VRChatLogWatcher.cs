using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VRCGroupSwitcher.Services
{
    public class VRCInstanceInfo
    {
        public string WorldId { get; set; } = string.Empty;
        public string InstanceId { get; set; } = string.Empty;
        public string? WorldName { get; set; }
        public string? GroupId { get; set; }
        public string? GroupAccessType { get; set; } // "members" (Group Only), "plus" (Group+), "public" (Group Public)
        public bool IsGroupInstance => !string.IsNullOrEmpty(GroupId);

        public string GroupAccessTypeName => GroupAccessType switch
        {
            "members" => "グループオンリー",
            "plus" => "グループプラス",
            "public" => "グループパブリック",
            _ => (!string.IsNullOrEmpty(GroupId) ? "グループ" : "")
        };

        public string RawInstanceString { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class VRChatLogWatcher : IDisposable
    {
        private static readonly Regex JoiningRegex = new(
            @"\[Behaviour\] Joining (wrld_[a-zA-Z0-9\-]+):([0-9a-zA-Z_~()\-]+)",
            RegexOptions.Compiled
        );

        private static readonly Regex RoomNameRegex = new(
            @"\[Behaviour\] (?:Entering Room|Joining or Creating Room):\s*(.+)$",
            RegexOptions.Compiled
        );

        private static readonly Regex GroupTagRegex = new(
            @"~group\((grp_[a-zA-Z0-9\-]+)\)",
            RegexOptions.Compiled
        );

        private static readonly Regex GroupAccessTypeRegex = new(
            @"~groupAccessType\(([a-zA-Z0-9]+)\)",
            RegexOptions.Compiled
        );

        private static readonly Regex LeftRoomRegex = new(
            @"\[Behaviour\] (OnLeftRoom|Leaving room|VRCApplication: OnApplicationQuit)",
            RegexOptions.Compiled
        );

        private readonly System.Threading.Timer _pollTimer;
        private string? _currentLogFilePath;
        private string? _pendingWorldName;
        private long _lastFilePosition = 0;
        private readonly object _lock = new();

        public event Action<VRCInstanceInfo>? InstanceChanged;
        public event Action? LeftRoom;
        public event Action<string, Models.LogLevel>? LogMessage;

        public string? CustomDirectoryPath { get; set; }
        public bool IsVRChatRunning { get; private set; }
        public VRCInstanceInfo? CurrentInstance { get; private set; }

        public VRChatLogWatcher()
        {
            _pollTimer = new System.Threading.Timer(OnPoll, null, 500, 500);
        }

        public string GetLogDirectory()
        {
            if (!string.IsNullOrEmpty(CustomDirectoryPath) && Directory.Exists(CustomDirectoryPath))
            {
                return CustomDirectoryPath;
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "AppData", "LocalLow", "VRChat", "VRChat");
        }

        private void OnPoll(object? state)
        {
            lock (_lock)
            {
                try
                {
                    string dir = GetLogDirectory();
                    if (!Directory.Exists(dir))
                    {
                        return;
                    }

                    // Look for latest output_log_*.txt by filename (output_log_YYYY-MM-DD_HH-mm-ss.txt is alphabetical)
                    var dirInfo = new DirectoryInfo(dir);
                    var latestFile = dirInfo.GetFiles("output_log_*.txt")
                        .OrderByDescending(f => f.Name)
                        .FirstOrDefault();

                    if (latestFile == null)
                    {
                        return;
                    }

                    // Check if new log file started
                    if (_currentLogFilePath != latestFile.FullName)
                    {
                        _currentLogFilePath = latestFile.FullName;
                        try
                        {
                            using var initFs = new FileStream(latestFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            _lastFilePosition = Math.Max(0, initFs.Length - 100000); // Read last 100KB of new file
                        }
                        catch
                        {
                            _lastFilePosition = 0;
                        }
                        LogMessage?.Invoke($"VRChatログファイルを検出: {latestFile.Name}", Models.LogLevel.Info);
                    }

                    // Read appended lines (bypassing cached FileInfo.Length)
                    ReadLogChanges(_currentLogFilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Log watcher error: {ex.Message}");
                }
            }
        }

        private void ReadLogChanges(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long fileLength = fs.Length;

                if (fileLength < _lastFilePosition)
                {
                    // File truncated or rotated
                    _lastFilePosition = 0;
                }

                if (fileLength == _lastFilePosition)
                {
                    return; // No new data
                }

                fs.Seek(_lastFilePosition, SeekOrigin.Begin);

                long bytesToRead = fileLength - _lastFilePosition;
                if (bytesToRead <= 0) return;

                // Read chunk up to 2MB
                int bufferSize = (int)Math.Min(bytesToRead, 2 * 1024 * 1024);
                byte[] buffer = new byte[bufferSize];
                int bytesRead = fs.Read(buffer, 0, bufferSize);
                if (bytesRead <= 0) return;

                // Find last newline character (\n) so we only process completed lines
                int lastNewLineIndex = -1;
                for (int i = bytesRead - 1; i >= 0; i--)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        lastNewLineIndex = i;
                        break;
                    }
                }

                if (lastNewLineIndex == -1)
                {
                    // Incomplete line still being written by VRChat, wait for next tick
                    return;
                }

                // Decode only up to the last complete line
                string chunk = Encoding.UTF8.GetString(buffer, 0, lastNewLineIndex + 1);
                _lastFilePosition += (lastNewLineIndex + 1);

                using var stringReader = new StringReader(chunk);
                string? line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    ParseLine(line);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading log file: {ex.Message}");
            }
        }

        private void ParseLine(string line)
        {
            // Check Joining wrld_...
            var joiningMatch = JoiningRegex.Match(line);
            if (joiningMatch.Success)
            {
                string worldId = joiningMatch.Groups[1].Value;
                string instanceFull = joiningMatch.Groups[2].Value;

                string? groupId = null;
                var groupMatch = GroupTagRegex.Match(instanceFull);
                if (groupMatch.Success)
                {
                    groupId = groupMatch.Groups[1].Value;
                }

                string? groupAccessType = null;
                var accessMatch = GroupAccessTypeRegex.Match(instanceFull);
                if (accessMatch.Success)
                {
                    groupAccessType = accessMatch.Groups[1].Value.ToLowerInvariant();
                }
                else if (groupId != null)
                {
                    // groupAccessTypeタグがない場合はデフォルトでmembers
                    groupAccessType = "members";
                }

                var info = new VRCInstanceInfo
                {
                    WorldId = worldId,
                    InstanceId = instanceFull,
                    GroupId = groupId,
                    GroupAccessType = groupAccessType,
                    WorldName = _pendingWorldName,
                    RawInstanceString = $"{worldId}:{instanceFull}",
                    Timestamp = DateTime.Now
                };

                CurrentInstance = info;
                IsVRChatRunning = true;

                if (!string.IsNullOrEmpty(info.WorldName))
                {
                    LogMessage?.Invoke($"ワールドに入室: {info.WorldName}", Models.LogLevel.Info);
                }

                InstanceChanged?.Invoke(info);
                return;
            }

            // Check Entering Room or Joining or Creating Room
            var roomMatch = RoomNameRegex.Match(line);
            if (roomMatch.Success)
            {
                string worldName = roomMatch.Groups[1].Value.Trim();
                _pendingWorldName = worldName;

                if (CurrentInstance != null)
                {
                    if (CurrentInstance.WorldName != worldName)
                    {
                        CurrentInstance.WorldName = worldName;
                        LogMessage?.Invoke($"ワールドに入室: {worldName}", Models.LogLevel.Info);
                        InstanceChanged?.Invoke(CurrentInstance);
                    }
                }
                return;
            }

            // Check Left Room / Disconnected
            if (LeftRoomRegex.IsMatch(line))
            {
                if (CurrentInstance != null)
                {
                    LogMessage?.Invoke($"ワールドから退出しました: {CurrentInstance.WorldName ?? CurrentInstance.WorldId}", Models.LogLevel.Info);
                }
                CurrentInstance = null;
                _pendingWorldName = null;
                LeftRoom?.Invoke();
            }
        }

        public void Dispose()
        {
            _pollTimer.Dispose();
        }
    }
}
