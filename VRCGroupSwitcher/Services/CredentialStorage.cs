using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VRCGroupSwitcher.Services
{
    public class StoredCredentials
    {
        public string? AuthCookie { get; set; }
        public string? TwoFactorCookie { get; set; }
        public string? SavedUsername { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public static class CredentialStorage
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VRCGroupStatusSwitcher"
        );
        private static readonly string LocalAppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VRCGroupStatusSwitcher"
        );
        private static readonly string BaseDirFolder = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string[] CandidateFolders = new[]
        {
            AppDataFolder,
            LocalAppDataFolder,
            BaseDirFolder
        };

        public static event Action<string, Models.LogLevel>? StorageLog;

        private static string GetActiveFilePath()
        {
            foreach (var folder in CandidateFolders)
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }
                    return Path.Combine(folder, "auth.dat");
                }
                catch { }
            }
            return Path.Combine(BaseDirFolder, "auth.dat");
        }

        public static bool SaveCredentials(string? authCookie, string? twoFactorCookie, string? username = null)
        {
            if (string.IsNullOrEmpty(authCookie) && string.IsNullOrEmpty(twoFactorCookie))
            {
                return false;
            }

            string filePath = GetActiveFilePath();

            try
            {
                var existing = LoadCredentials();
                string? authToSave = !string.IsNullOrEmpty(authCookie) ? authCookie : existing?.AuthCookie;
                string? twoFactorToSave = !string.IsNullOrEmpty(twoFactorCookie) ? twoFactorCookie : existing?.TwoFactorCookie;
                string? userToSave = !string.IsNullOrEmpty(username) ? username : existing?.SavedUsername;

                var data = new StoredCredentials
                {
                    AuthCookie = authToSave,
                    TwoFactorCookie = twoFactorToSave,
                    SavedUsername = userToSave,
                    SavedAt = DateTime.UtcNow
                };

                string json = JsonSerializer.Serialize(data);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(filePath, encryptedBytes);
                StorageLog?.Invoke($"認証情報を暗号化保存しました (auth={authToSave?.Substring(0, Math.Min(10, authToSave?.Length ?? 0))}..., 2FA={(twoFactorToSave != null ? "あり" : "なし")})", Models.LogLevel.Success);
                return true;
            }
            catch (Exception ex)
            {
                StorageLog?.Invoke($"認証情報の保存に失敗しました: {ex.Message}", Models.LogLevel.Error);
                return false;
            }
        }

        public static StoredCredentials? LoadCredentials()
        {
            foreach (var folder in CandidateFolders)
            {
                string path = Path.Combine(folder, "auth.dat");
                try
                {
                    if (File.Exists(path))
                    {
                        byte[] encryptedBytes = File.ReadAllBytes(path);
                        byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                        string json = Encoding.UTF8.GetString(plainBytes);
                        var creds = JsonSerializer.Deserialize<StoredCredentials>(json);
                        if (creds != null && (!string.IsNullOrEmpty(creds.AuthCookie) || !string.IsNullOrEmpty(creds.TwoFactorCookie)))
                        {
                            string primaryFile = Path.Combine(AppDataFolder, "auth.dat");
                            if (folder != AppDataFolder && !File.Exists(primaryFile))
                            {
                                try
                                {
                                    if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                                    File.WriteAllBytes(primaryFile, encryptedBytes);
                                }
                                catch { }
                            }
                            return creds;
                        }
                    }
                }
                catch (Exception ex)
                {
                    StorageLog?.Invoke($"認証ファイルの読み込みエラー ({Path.GetFileName(path)}): {ex.Message}", Models.LogLevel.Warning);
                }
            }

            return null;
        }

        public static void ClearCredentials()
        {
            foreach (var folder in CandidateFolders)
            {
                string path = Path.Combine(folder, "auth.dat");
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        StorageLog?.Invoke($"保存されていた認証情報を削除しました: {path}", Models.LogLevel.Info);
                    }
                }
                catch { }
            }
        }
    }
}
