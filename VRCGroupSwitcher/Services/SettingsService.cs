using System;
using System.IO;
using System.Text.Json;
using VRCGroupSwitcher.Models;

namespace VRCGroupSwitcher.Services
{
    public class SettingsService
    {
        public static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRCGroupStatusSwitcher"
        );

        public static readonly string LocalAppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRCGroupStatusSwitcher"
        );

        public static readonly string BaseDirFolder = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public AppSettings CurrentSettings { get; private set; }
        public string? ActiveSettingsFilePath { get; private set; }

        public event EventHandler<AppSettings>? SettingsChanged;
        public event Action<string, LogLevel>? SettingsLog;

        public SettingsService()
        {
            // 1. Ensure %APPDATA%\VRCGroupStatusSwitcher directory is created first
            EnsurePrimaryDirectory();

            // 2. Load existing settings (prioritize AppData, then LocalAppData, then BaseDir)
            CurrentSettings = Load();

            // 3. Save to primary AppData location immediately if not already there,
            //    or migrate from BaseDir to AppData.
            string primaryFilePath = Path.Combine(AppDataFolder, "config.json");
            if (!File.Exists(primaryFilePath) || ActiveSettingsFilePath != primaryFilePath)
            {
                Save();
            }
        }

        private static void EnsurePrimaryDirectory()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create primary AppData folder: {ex.Message}");
            }
        }

        public AppSettings Load()
        {
            string[] searchFolders = { AppDataFolder, LocalAppDataFolder, BaseDirFolder };

            foreach (var folder in searchFolders)
            {
                try
                {
                    string filePath = Path.Combine(folder, "config.json");
                    if (File.Exists(filePath))
                    {
                        string json = File.ReadAllText(filePath);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json);
                        if (settings != null)
                        {
                            ActiveSettingsFilePath = filePath;
                            CurrentSettings = settings;
                            return settings;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings from {folder}: {ex.Message}");
                }
            }

            CurrentSettings = new AppSettings();
            return CurrentSettings;
        }

        public void Save()
        {
            string[] targetFolders = { AppDataFolder, LocalAppDataFolder, BaseDirFolder };

            foreach (var folder in targetFolders)
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    string filePath = Path.Combine(folder, "config.json");
                    string json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
                    File.WriteAllText(filePath, json);

                    ActiveSettingsFilePath = filePath;
                    SettingsChanged?.Invoke(this, CurrentSettings);
                    SettingsLog?.Invoke($"設定を保存しました ({filePath})", LogLevel.Info);
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings to {folder}: {ex.Message}");
                }
            }

            SettingsLog?.Invoke("設定の保存に失敗しました。", LogLevel.Error);
        }

        public void Update(Action<AppSettings> updateAction)
        {
            updateAction(CurrentSettings);
            Save();
        }
    }
}
