using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VRCGroupSwitcher.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string ReleaseTitle { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string BoothDownloadUrl { get; set; } = "https://nezumi-tech.booth.pm/items/8975157?utm_source=update_notice";
        public string? ErrorMessage { get; set; }
    }

    public class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public string? PublishedAt { get; set; }
    }

    public class UpdateCheckerService : IDisposable
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/nezumi-tech/VRChat_Group_Status_Switcher/releases/latest";
        public const string DefaultBoothUrl = "https://nezumi-tech.booth.pm/items/8975157?utm_source=update_notice";

        private readonly HttpClient _httpClient;

        public UpdateCheckerService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "VRCGroupSwitcher-UpdateChecker");
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// 現在のアセンブリの InformationalVersion を取得します (コミットハッシュ等を除去したプレーン値)
        /// </summary>
        public static string GetCurrentInformationalVersion()
        {
            var attr = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string raw = attr?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";
            return raw.Split('+')[0].Trim();
        }

        /// <summary>
        /// GitHub Releases API を照会して最新バージョンを確認します
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            string currentVersion = GetCurrentInformationalVersion();
            var result = new UpdateInfo
            {
                CurrentVersion = currentVersion,
                BoothDownloadUrl = DefaultBoothUrl
            };

            try
            {
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"GitHub API 応答エラー: {response.StatusCode}";
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubReleaseResponse>(json);
                if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                {
                    result.ErrorMessage = "リリース情報が見つかりませんでした。";
                    return result;
                }

                string latestTag = release.TagName.Trim();
                string cleanedLatest = CleanVersionString(latestTag);
                string cleanedCurrent = CleanVersionString(currentVersion);

                result.LatestVersion = cleanedLatest;
                result.ReleaseTitle = release.Name ?? latestTag;
                result.ReleaseNotes = release.Body ?? "";
                result.IsUpdateAvailable = IsNewerVersion(cleanedCurrent, cleanedLatest);

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"アップデート確認エラー: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// バージョン文字列を比較し、latest が current より新しいかを判定します
        /// </summary>
        public static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            if (Version.TryParse(currentVersion, out var cur) && Version.TryParse(latestVersion, out var lat))
            {
                return lat > cur;
            }

            // フォールバック: ピリオド区切りの数値比較
            var curParts = currentVersion.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            var latParts = latestVersion.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();

            int maxLen = Math.Max(curParts.Length, latParts.Length);
            for (int i = 0; i < maxLen; i++)
            {
                int cVal = i < curParts.Length ? curParts[i] : 0;
                int lVal = i < latParts.Length ? latParts[i] : 0;
                if (lVal > cVal) return true;
                if (lVal < cVal) return false;
            }

            return false;
        }

        private static string CleanVersionString(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";

            // 先頭の 'v' や 'V' を除去
            string v = version.Trim().TrimStart('v', 'V');

            // '+' や '-' 以降のメタデータを除去
            int plusIdx = v.IndexOf('+');
            if (plusIdx >= 0) v = v.Substring(0, plusIdx);

            int minusIdx = v.IndexOf('-');
            if (minusIdx >= 0) v = v.Substring(0, minusIdx);

            return v.Trim();
        }

        /// <summary>
        /// BOOTHのダウンロードページをデフォルトブラウザで開きます
        /// </summary>
        public static void OpenBoothDownloadPage(string? url = null)
        {
            string targetUrl = !string.IsNullOrEmpty(url) ? url : DefaultBoothUrl;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open browser: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
