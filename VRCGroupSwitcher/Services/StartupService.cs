using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace VRCGroupSwitcher.Services
{
    public static class StartupService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "VRCGroupSwitcher";

        /// <summary>
        /// 現在Windowsスタートアップに登録されているかを確認します。
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                var value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check startup registry: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Windowsスタートアップの登録・解除を切り替えます。
        /// </summary>
        public static bool SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return false;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }

                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        // スタートアップ時は --tray 引数を付けて静かにタスクトレイ常駐起動
                        string command = $"\"{exePath}\" --tray";
                        key.SetValue(AppName, command);
                        return true;
                    }
                    return false;
                }
                else
                {
                    key.DeleteValue(AppName, false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update startup registry: {ex.Message}");
                return false;
            }
        }
    }
}
