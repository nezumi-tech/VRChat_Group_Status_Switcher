using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VRCGroupSwitcher.Models;

namespace VRCGroupSwitcher.Services
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public bool RequiresTwoFactor { get; set; }
        public string? TwoFactorType { get; set; } // "totp", "emailOtp", or "otp"
        public string? ErrorMessage { get; set; }
        public VRCUser? User { get; set; }
    }

    public class VRChatApiClient : IDisposable
    {
        private const string BaseUrl = "https://api.vrchat.cloud";
        private const string UserAgent = "VRCGroupStatusSwitcher/1.0.0 (Windows; Contact: X: @nezumi_tech)";

        private HttpClientHandler _handler = null!;
        private HttpClient _client = null!;
        private CookieContainer _cookies = null!;

        // Explicit tracking of current active session cookies
        private string? _currentAuthCookie;
        private string? _currentTwoFactorCookie;

        public VRCUser? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null;

        public event Action<string, LogLevel>? ApiLog;

        public VRChatApiClient()
        {
            ResetClient();
        }

        /// <summary>
        /// Resets HttpClient and CookieContainer to a completely clean state,
        /// or initializes them with a fresh set of restored cookies.
        /// </summary>
        private void ResetClient(CookieContainer? initialCookies = null)
        {
            try { _client?.Dispose(); } catch { }
            try { _handler?.Dispose(); } catch { }

            _cookies = initialCookies ?? new CookieContainer();
            _handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            _client = new HttpClient(_handler)
            {
                BaseAddress = new Uri(BaseUrl)
            };

            _client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Synchronizes auth and twoFactorAuth cookies into the CookieContainer under api.vrchat.cloud.
        /// </summary>
        private void SetCookiesInContainer(string? auth, string? twoFactor)
        {
            var baseUri = new Uri(BaseUrl);
            if (!string.IsNullOrEmpty(auth))
            {
                _currentAuthCookie = auth;
                try { _cookies.Add(baseUri, new Cookie("auth", auth, "/", "api.vrchat.cloud")); } catch { }
                try { _cookies.Add(baseUri, new Cookie("auth", auth)); } catch { }
            }
            if (!string.IsNullOrEmpty(twoFactor))
            {
                _currentTwoFactorCookie = twoFactor;
                try { _cookies.Add(baseUri, new Cookie("twoFactorAuth", twoFactor, "/", "api.vrchat.cloud")); } catch { }
                try { _cookies.Add(baseUri, new Cookie("twoFactorAuth", twoFactor)); } catch { }
            }
        }

        /// <summary>
        /// Extracts auth and twoFactorAuth cookies directly from HTTP Set-Cookie response headers.
        /// </summary>
        private void CaptureCookiesFromResponse(HttpResponseMessage response)
        {
            try
            {
                if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    foreach (var sc in setCookies)
                    {
                        if (sc.StartsWith("auth=", StringComparison.OrdinalIgnoreCase))
                        {
                            int semi = sc.IndexOf(';');
                            string val = (semi > 0 ? sc.Substring(5, semi - 5) : sc.Substring(5)).Trim();
                            if (!string.IsNullOrEmpty(val))
                            {
                                _currentAuthCookie = val;
                                SetCookiesInContainer(val, null);
                            }
                        }
                        else if (sc.StartsWith("twoFactorAuth=", StringComparison.OrdinalIgnoreCase))
                        {
                            int semi = sc.IndexOf(';');
                            string val = (semi > 0 ? sc.Substring(14, semi - 14) : sc.Substring(14)).Trim();
                            if (!string.IsNullOrEmpty(val))
                            {
                                _currentTwoFactorCookie = val;
                                SetCookiesInContainer(null, val);
                            }
                        }
                    }
                }

                // Also check CookieContainer in case handler stored them automatically
                foreach (Cookie c in _cookies.GetAllCookies())
                {
                    if (c.Name.Equals("auth", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.Value))
                    {
                        _currentAuthCookie ??= c.Value;
                    }
                    if (c.Name.Equals("twoFactorAuth", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.Value))
                    {
                        _currentTwoFactorCookie ??= c.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CaptureCookies error: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to restore session using stored credentials (auth cookie & twoFactorAuth cookie).
        /// Both cookies are strictly required to bypass the 2FA challenge on subsequent launches.
        /// </summary>
        public async Task<bool> TryRestoreSessionAsync()
        {
            var stored = CredentialStorage.LoadCredentials();
            if (stored == null || string.IsNullOrEmpty(stored.AuthCookie))
            {
                ApiLog?.Invoke("保存されたログインセッションはありません。", LogLevel.Info);
                return false;
            }

            ApiLog?.Invoke($"保存されたセッション情報を読み込みました (ユーザー: {stored.SavedUsername ?? "不明"}, auth: 保持, twoFactorAuth: {(!string.IsNullOrEmpty(stored.TwoFactorCookie) ? "保持" : "なし")})。サーバー検証中...", LogLevel.Info);

            try
            {
                var container = new CookieContainer();
                ResetClient(container);
                SetCookiesInContainer(stored.AuthCookie, stored.TwoFactorCookie);

                var response = await _client.GetAsync("/api/1/auth/user");
                CaptureCookiesFromResponse(response);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!json.Contains("\"requiresTwoFactorAuth\""))
                    {
                        var user = JsonSerializer.Deserialize<VRCUser>(json);
                        if (user != null && !string.IsNullOrEmpty(user.Id))
                        {
                            CurrentUser = user;
                            SaveCurrentCookies(user.Username);
                            ApiLog?.Invoke($"自動ログインに成功しました！(ようこそ、{CurrentUser.DisplayName} さん)", LogLevel.Success);
                            return true;
                        }
                    }
                    else
                    {
                        ApiLog?.Invoke("セッション有効期限が切れ、2段階認証が再要求されました。再ログインしてください。", LogLevel.Warning);
                    }
                }
                else
                {
                    ApiLog?.Invoke($"セッション有効期限が切れています ({response.StatusCode})。再ログインしてください。", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                ApiLog?.Invoke($"セッション復元エラー: {ex.Message}", LogLevel.Error);
            }

            // セッション復元に失敗した場合は、古い不整合Cookieを残さないよう完全クリア
            _currentAuthCookie = null;
            _currentTwoFactorCookie = null;
            ResetClient();
            return false;
        }

        /// <summary>
        /// Logs in using username and password via HTTP Basic Auth.
        /// Password is never saved to disk.
        /// </summary>
        public async Task<LoginResult> LoginAsync(string username, string password, bool rememberCredentials = true)
        {
            try
            {
                // 新規ログイン開始時は、以前の古いCookieをすべてリセット
                _currentAuthCookie = null;
                _currentTwoFactorCookie = null;
                ResetClient();

                string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}"));
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/1/auth/user");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                ApiLog?.Invoke($"VRChatへログイン要求を送信中 ({username})...", LogLevel.Info);
                var response = await _client.SendAsync(request);
                CaptureCookiesFromResponse(response);

                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (json.Contains("\"requiresTwoFactorAuth\""))
                    {
                        // requiresTwoFactorAuth 配列を解析
                        string twoFactorType = "totp";
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("requiresTwoFactorAuth", out var arr) && arr.ValueKind == JsonValueKind.Array)
                            {
                                var types = new List<string>();
                                foreach (var item in arr.EnumerateArray())
                                {
                                    types.Add(item.GetString() ?? "");
                                }
                                ApiLog?.Invoke($"サーバー要求2FA方式: [{string.Join(", ", types)}]", LogLevel.Info);

                                // TOTP (認証アプリ) を最優先
                                if (types.Contains("totp", StringComparer.OrdinalIgnoreCase))
                                {
                                    twoFactorType = "totp";
                                }
                                else if (types.Contains("emailOtp", StringComparer.OrdinalIgnoreCase) || types.Contains("email", StringComparer.OrdinalIgnoreCase))
                                {
                                    twoFactorType = "emailOtp";
                                }
                                else if (types.Contains("otp", StringComparer.OrdinalIgnoreCase))
                                {
                                    twoFactorType = "otp";
                                }
                                else if (types.Count > 0)
                                {
                                    twoFactorType = types[0];
                                }
                            }
                        }
                        catch { }

                        ApiLog?.Invoke($"2段階認証 (2FA: {twoFactorType}) が必要です。認証コードを入力してください。(authCookie保持: {!string.IsNullOrEmpty(_currentAuthCookie)})", LogLevel.Info);

                        return new LoginResult
                        {
                            Success = false,
                            RequiresTwoFactor = true,
                            TwoFactorType = twoFactorType
                        };
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var user = JsonSerializer.Deserialize<VRCUser>(json);
                        if (user != null && !string.IsNullOrEmpty(user.Id))
                        {
                            CurrentUser = user;
                            SaveCurrentCookies(user.Username);
                            ApiLog?.Invoke($"ログインに成功しました！(ようこそ、{user.DisplayName} さん)", LogLevel.Success);
                            return new LoginResult { Success = true, User = user };
                        }
                    }

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return new LoginResult
                        {
                            Success = false,
                            ErrorMessage = "ユーザー名またはパスワードが正しくありません。"
                        };
                    }
                }

                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"ログイン失敗: {response.StatusCode} {json}"
                };
            }
            catch (Exception ex)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"通信エラー: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Verifies 2FA TOTP or Email OTP code with automatic fallback across endpoints.
        /// </summary>
        public async Task<LoginResult> VerifyTwoFactorAsync(string code, string twoFactorType = "totp")
        {
            try
            {
                // 全角数字を半角に正規化し、数字のみ抽出（スペースやハイフンを除去）
                string cleanCode = "";
                foreach (char ch in code)
                {
                    if (ch >= '0' && ch <= '9')
                    {
                        cleanCode += ch;
                    }
                    else if (ch >= '０' && ch <= '９')
                    {
                        cleanCode += (char)(ch - '０' + '0');
                    }
                }

                if (string.IsNullOrEmpty(cleanCode))
                {
                    return new LoginResult
                    {
                        Success = false,
                        ErrorMessage = "有効な数字コードが入力されていません。"
                    };
                }

                // 試行するエンドポイントの候補リスト（指定タイプを最優先、ダメなら代替候補を自動試行）
                var candidateEndpoints = new List<(string Name, string Path)>();
                if (twoFactorType.Equals("emailOtp", StringComparison.OrdinalIgnoreCase) || twoFactorType.Equals("email", StringComparison.OrdinalIgnoreCase))
                {
                    candidateEndpoints.Add(("emailOtp", "/api/1/auth/twofactorauth/emailotp/verify"));
                    candidateEndpoints.Add(("totp", "/api/1/auth/twofactorauth/totp/verify"));
                    candidateEndpoints.Add(("otp", "/api/1/auth/twofactorauth/otp/verify"));
                }
                else
                {
                    candidateEndpoints.Add(("totp", "/api/1/auth/twofactorauth/totp/verify"));
                    candidateEndpoints.Add(("emailOtp", "/api/1/auth/twofactorauth/emailotp/verify"));
                    candidateEndpoints.Add(("otp", "/api/1/auth/twofactorauth/otp/verify"));
                }

                string lastErrorJson = "";
                HttpStatusCode lastStatusCode = HttpStatusCode.BadRequest;

                foreach (var candidate in candidateEndpoints)
                {
                    ApiLog?.Invoke($"2段階認証コード送信中 ({candidate.Name}: {candidate.Path}, コード: {cleanCode.Length}桁)...", LogLevel.Info);

                    var payload = new { code = cleanCode };
                    string jsonBody = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    // auth Cookie を確実にコンテナへ同期
                    if (!string.IsNullOrEmpty(_currentAuthCookie))
                    {
                        SetCookiesInContainer(_currentAuthCookie, null);
                    }

                    var response = await _client.PostAsync(candidate.Path, content);
                    CaptureCookiesFromResponse(response);

                    string verifyJson = await response.Content.ReadAsStringAsync();
                    ApiLog?.Invoke($"2FA検証応答 [{candidate.Name}] (HTTP {(int)response.StatusCode}): {verifyJson}", LogLevel.Info);

                    if (response.IsSuccessStatusCode)
                    {
                        if (verifyJson.Contains("\"verified\": false") || verifyJson.Contains("\"verified\":false"))
                        {
                            ApiLog?.Invoke($"2FAコードが不一致でした [{candidate.Name}] (verified: false)。次の候補を試行...", LogLevel.Warning);
                            lastErrorJson = verifyJson;
                            continue;
                        }

                        // 2FA検証完了！authCookieとtwoFactorAuthの両方をサーバーから受け取り完了
                        ApiLog?.Invoke($"2FA検証成功！([{candidate.Name}], twoFactorAuth保持: {!string.IsNullOrEmpty(_currentTwoFactorCookie)})。ユーザープロファイルを取得中...", LogLevel.Success);

                        var userResponse = await _client.GetAsync("/api/1/auth/user");
                        CaptureCookiesFromResponse(userResponse);

                        if (userResponse.IsSuccessStatusCode)
                        {
                            string userJson = await userResponse.Content.ReadAsStringAsync();
                            var user = JsonSerializer.Deserialize<VRCUser>(userJson);
                            if (user != null && !string.IsNullOrEmpty(user.Id))
                            {
                                CurrentUser = user;
                                SaveCurrentCookies(user.Username);
                                ApiLog?.Invoke($"ログイン完了！(ようこそ、{user.DisplayName} さん)", LogLevel.Success);
                                return new LoginResult { Success = true, User = user };
                            }
                        }
                        else
                        {
                            string userErr = await userResponse.Content.ReadAsStringAsync();
                            ApiLog?.Invoke($"ユーザー情報取得失敗 ({userResponse.StatusCode}): {userErr}", LogLevel.Error);
                            return new LoginResult
                            {
                                Success = false,
                                ErrorMessage = $"2FA成功後のユーザー情報取得に失敗しました ({userResponse.StatusCode}: {userErr})"
                            };
                        }
                    }
                    else
                    {
                        lastStatusCode = response.StatusCode;
                        lastErrorJson = verifyJson;
                        // もし400で有効なコードでないと言われた場合、次の候補（emailOtp等）へフォールバック
                    }
                }

                // すべてのエンドポイントで不一致だった場合
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"2段階認証失敗 (HTTP {(int)lastStatusCode}: {lastErrorJson})"
                };
            }
            catch (Exception ex)
            {
                ApiLog?.Invoke($"2FA検証例外: {ex.Message}", LogLevel.Error);
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = $"2FA認証エラー: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Saves session cookies (both auth & twoFactorAuth) into DPAPI encrypted storage.
        /// Password is NEVER saved.
        /// </summary>
        private void SaveCurrentCookies(string? username)
        {
            try
            {
                string? auth = _currentAuthCookie;
                string? twoFactor = _currentTwoFactorCookie;

                var baseUri = new Uri(BaseUrl);
                var cookies = _cookies.GetCookies(baseUri);
                auth ??= cookies["auth"]?.Value;
                twoFactor ??= cookies["twoFactorAuth"]?.Value;

                foreach (Cookie c in _cookies.GetAllCookies())
                {
                    if (c.Name.Equals("auth", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.Value))
                    {
                        auth ??= c.Value;
                    }
                    if (c.Name.Equals("twoFactorAuth", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(c.Value))
                    {
                        twoFactor ??= c.Value;
                    }
                }

                if (!string.IsNullOrEmpty(auth))
                {
                    CredentialStorage.SaveCredentials(auth, twoFactor, username);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save cookies: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes current user information.
        /// </summary>
        public async Task<VRCUser?> RefreshUserAsync()
        {
            try
            {
                var response = await _client.GetAsync("/api/1/auth/user");
                CaptureCookiesFromResponse(response);
                SaveCurrentCookies(CurrentUser?.Username);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    CurrentUser = JsonSerializer.Deserialize<VRCUser>(json);
                    return CurrentUser;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh user: {ex.Message}");
            }
            return CurrentUser;
        }

        /// <summary>
        /// Retrieves the list of groups the current user belongs to.
        /// </summary>
        public async Task<List<VRCGroup>> GetUserGroupsAsync()
        {
            if (CurrentUser == null) return new List<VRCGroup>();

            try
            {
                var response = await _client.GetAsync($"/api/1/users/{CurrentUser.Id}/groups");
                CaptureCookiesFromResponse(response);
                SaveCurrentCookies(CurrentUser.Username);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var groups = JsonSerializer.Deserialize<List<VRCGroup>>(json);
                    return groups ?? new List<VRCGroup>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch groups: {ex.Message}");
            }

            return new List<VRCGroup>();
        }

        /// <summary>
        /// Sets a group as the represented group (displays its banner).
        /// </summary>
        public async Task<(bool success, string message)> SetRepresentedGroupAsync(string groupId)
        {
            try
            {
                var payload = new { isRepresenting = true };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _client.PutAsync($"/api/1/groups/{groupId}/representation", content);
                CaptureCookiesFromResponse(response);
                SaveCurrentCookies(CurrentUser?.Username);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "グループバナーを設定しました。");
                }

                string err = await response.Content.ReadAsStringAsync();
                return (false, $"グループバナー変更失敗: {response.StatusCode} {err}");
            }
            catch (Exception ex)
            {
                return (false, $"通信エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the currently represented group (no banner displayed).
        /// </summary>
        public async Task<(bool success, string message)> ClearRepresentedGroupAsync(string currentGroupId)
        {
            try
            {
                var payload = new { isRepresenting = false };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _client.PutAsync($"/api/1/groups/{currentGroupId}/representation", content);
                CaptureCookiesFromResponse(response);
                SaveCurrentCookies(CurrentUser?.Username);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "グループバナーの掲示を解除しました。");
                }

                string err = await response.Content.ReadAsStringAsync();
                return (false, $"グループバナー解除失敗: {response.StatusCode} {err}");
            }
            catch (Exception ex)
            {
                return (false, $"通信エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates user status and status message.
        /// </summary>
        public async Task<(bool success, string message)> UpdateStatusAsync(string? status, string? statusDescription)
        {
            if (CurrentUser == null) return (false, "未ログインです。");

            try
            {
                var payload = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(status) && status != "unchanged")
                {
                    payload["status"] = status;
                }

                if (statusDescription != null)
                {
                    payload["statusDescription"] = statusDescription;
                }

                if (payload.Count == 0)
                {
                    return (true, "変更項目なし");
                }

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _client.PutAsync($"/api/1/users/{CurrentUser.Id}", content);
                CaptureCookiesFromResponse(response);
                SaveCurrentCookies(CurrentUser.Username);

                if (response.IsSuccessStatusCode)
                {
                    if (payload.ContainsKey("status"))
                    {
                        CurrentUser.Status = status!;
                    }
                    if (payload.ContainsKey("statusDescription"))
                    {
                        CurrentUser.StatusDescription = statusDescription!;
                    }
                    return (true, "ステータスを更新しました。");
                }

                string err = await response.Content.ReadAsStringAsync();
                return (false, $"ステータス更新失敗: {response.StatusCode} {err}");
            }
            catch (Exception ex)
            {
                return (false, $"通信エラー: {ex.Message}");
            }
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _worldNameCache = new();

        /// <summary>
        /// ワールドIDからワールド名を取得します（キャッシュ付き）。
        /// </summary>
        public async Task<string?> GetWorldNameAsync(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return null;
            if (_worldNameCache.TryGetValue(worldId, out var cachedName)) return cachedName;
            if (!IsAuthenticated) return null;

            try
            {
                var response = await _client.GetAsync($"/api/1/worlds/{worldId}");
                CaptureCookiesFromResponse(response);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        string name = nameProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(name))
                        {
                            _worldNameCache[worldId] = name;
                            return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get world name for {worldId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Logs out the user and clears stored credentials.
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                await _client.PutAsync("/api/1/logout", null);
            }
            catch { }

            CurrentUser = null;
            _currentAuthCookie = null;
            _currentTwoFactorCookie = null;
            CredentialStorage.ClearCredentials();
            ResetClient();
        }

        public void Dispose()
        {
            _client.Dispose();
            _handler.Dispose();
        }
    }
}
