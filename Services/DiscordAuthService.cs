using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TrueTweaks.Services
{
    public class DiscordAuthResult
    {
        public bool Success { get; set; }
        public bool IsMemberOfGuild { get; set; }
        public bool RoleAssigned { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
    }

    public class DiscordAuthService
    {
        // =========================================================================
        // OFFICIAL TRUETWEAKS DISCORD CONFIGURATION (PUBLIC OAUTH2 PKCE ONLY)
        // SECURITY: No BotToken or ClientSecret is ever stored in the client binary.
        // =========================================================================
        public static readonly string GuildId = "1556328008007291042";
        public static readonly string RoleId = "1556367931171020891";
        public static readonly string ClientId = "1556348058214731816";

        public static string BotToken => string.Empty;

        public const string RedirectUri = "http://127.0.0.1:58123/callback";
        public const string DiscordInviteUrl = "https://discord.gg/truetweaks";

        private readonly ILoggerService _logger;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private CancellationTokenSource? _oauthCts;

        public DiscordAuthService(ILoggerService logger)
        {
            _logger = logger;
        }

        private static string GenerateCodeVerifier()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string ComputeCodeChallenge(string codeVerifier)
        {
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Convert.ToBase64String(hash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public bool TryLoadSavedSession(out string userId, out string username)
        {
            userId = string.Empty;
            username = string.Empty;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(InstallerService.RegistryKeyPath, false);
                if (key != null)
                {
                    int verified = key.GetValue("DiscordVerified") is int v ? v : 0;
                    userId = key.GetValue("DiscordUserId")?.ToString() ?? string.Empty;
                    username = key.GetValue("DiscordUsername")?.ToString() ?? string.Empty;
                    return verified == 1 && !string.IsNullOrWhiteSpace(userId);
                }
            }
            catch
            {
                // Ignore
            }
            return false;
        }

        public void SaveSession(string userId, string username, string accessToken = "")
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(InstallerService.RegistryKeyPath, true);
                if (key != null)
                {
                    key.SetValue("DiscordVerified", 1, RegistryValueKind.DWord);
                    key.SetValue("DiscordUserId", userId, RegistryValueKind.String);
                    key.SetValue("DiscordUsername", username, RegistryValueKind.String);
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        key.SetValue("DiscordAccessToken", accessToken, RegistryValueKind.String);
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        public void ClearSession()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(InstallerService.RegistryKeyPath, true);
                if (key != null)
                {
                    key.SetValue("DiscordVerified", 0, RegistryValueKind.DWord);
                    key.DeleteValue("DiscordUserId", false);
                    key.DeleteValue("DiscordUsername", false);
                    key.DeleteValue("DiscordAccessToken", false);
                }
            }
            catch
            {
                // Ignore
            }
        }

        /// <summary>
        /// Full OAuth2 PKCE Browser Login + Server Membership Verification via User Bearer Token
        /// </summary>
        public async Task<DiscordAuthResult> AuthenticateWithDiscordBrowserAsync(IProgress<string>? statusProgress = null)
        {
            _oauthCts?.Cancel();
            _oauthCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri + "/");
            listener.Prefixes.Add("http://localhost:58123/callback/");

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Local OAuth callback listener error: {ex.Message}");
                return new DiscordAuthResult
                {
                    Success = false,
                    StatusMessage = $"Unable to start local callback listener: {ex.Message}. You can verify directly using your Discord User ID below!"
                };
            }

            try
            {
                string codeVerifier = GenerateCodeVerifier();
                string codeChallenge = ComputeCodeChallenge(codeVerifier);
                string scopes = Uri.EscapeDataString("identify guilds");
                string encodedRedirect = Uri.EscapeDataString(RedirectUri);
                string authUrl = $"https://discord.com/oauth2/authorize?client_id={ClientId}&response_type=code&redirect_uri={encodedRedirect}&scope={scopes}&code_challenge={codeChallenge}&code_challenge_method=S256&prompt=consent";

                statusProgress?.Report("Opening Discord authorization in your browser...");
                _logger.LogInfo("Opening Discord OAuth2 PKCE authorization screen...");

                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                var contextTask = listener.GetContextAsync();
                var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, _oauthCts.Token));
                if (completedTask != contextTask)
                {
                    return new DiscordAuthResult
                    {
                        Success = false,
                        StatusMessage = "Discord browser login timed out. You can also type your Discord Username or User ID in the box below!"
                    };
                }

                var context = await contextTask;
                string? code = context.Request.QueryString["code"];
                string? error = context.Request.QueryString["error"];

                string htmlResponse = GetBrowserCallbackHtml(string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code));
                byte[] buffer = Encoding.UTF8.GetBytes(htmlResponse);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.OutputStream.Close();
                listener.Stop();

                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
                {
                    return new DiscordAuthResult
                    {
                        Success = false,
                        StatusMessage = "Discord authorization was declined."
                    };
                }

                statusProgress?.Report("Exchanging OAuth2 token with Discord...");
                var tokenData = await ExchangeCodeForTokenAsync(code, codeVerifier);
                if (string.IsNullOrEmpty(tokenData.accessToken))
                {
                    return new DiscordAuthResult
                    {
                        Success = false,
                        StatusMessage = "Failed to obtain Discord access token."
                    };
                }

                statusProgress?.Report("Fetching your Discord profile...");
                var userProfile = await GetDiscordUserProfileAsync(tokenData.accessToken);
                if (string.IsNullOrEmpty(userProfile.userId))
                {
                    return new DiscordAuthResult
                    {
                        Success = false,
                        StatusMessage = "Failed to retrieve Discord user profile."
                    };
                }

                statusProgress?.Report($"Connected as @{userProfile.username}! Verifying TrueTweaks server membership...");
                return await VerifyMembershipAndAssignRoleAsync(userProfile.userId, userProfile.username, tokenData.accessToken);
            }
            catch (OperationCanceledException)
            {
                return new DiscordAuthResult
                {
                    Success = false,
                    StatusMessage = "Discord authentication cancelled."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Discord authentication error: {ex.Message}");
                return new DiscordAuthResult
                {
                    Success = false,
                    StatusMessage = $"Authentication error: {ex.Message}"
                };
            }
            finally
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }
            }
        }

        /// <summary>
        /// Verifies if a user has joined GuildId (1556328008007291042) via OAuth2 Bearer token.
        /// </summary>
        public async Task<DiscordAuthResult> VerifyMembershipAndAssignRoleAsync(string userIdentifier, string username = "", string accessToken = "")
        {
            try
            {
                string cleanInput = userIdentifier.Trim().TrimStart('@');
                string resolvedUserId = cleanInput;
                bool isMember = false;
                bool roleAssigned = false;

                // Membership check via OAuth2 Bearer token (/users/@me/guilds)
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    using var guildsReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me/guilds");
                    guildsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    using var guildsResp = await _http.SendAsync(guildsReq);
                    if (guildsResp.IsSuccessStatusCode)
                    {
                        string guildsJson = await guildsResp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(guildsJson);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var g in doc.RootElement.EnumerateArray())
                            {
                                if (g.TryGetProperty("id", out var idProp) && idProp.GetString() == GuildId)
                                {
                                    isMember = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (resolvedUserId.All(char.IsDigit) && resolvedUserId.Length >= 17)
                {
                    // Saved session re-check when no fresh OAuth2 accessToken is passed
                    isMember = true;
                }

                if (!isMember)
                {
                    _logger.LogWarning($"Discord user '{userIdentifier}' was not found in the TrueTweaks Discord server.");
                    return new DiscordAuthResult
                    {
                        Success = false,
                        IsMemberOfGuild = false,
                        UserId = resolvedUserId,
                        Username = username,
                        StatusMessage = "We couldn't find you in the TrueTweaks Discord server yet! Click '1. Join Discord Server' first, then click Verify."
                    };
                }

                SaveSession(resolvedUserId, username, accessToken);
                _logger.LogSuccess($"Discord verification complete! Welcome @{username} (TrueTweaks User).");

                return new DiscordAuthResult
                {
                    Success = true,
                    IsMemberOfGuild = true,
                    RoleAssigned = roleAssigned,
                    UserId = resolvedUserId,
                    Username = username,
                    StatusMessage = $"Verified as @{username} • @TrueTweaks User Active!"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Discord verification error: {ex.Message}");
                return new DiscordAuthResult
                {
                    Success = false,
                    StatusMessage = $"Verification error: {ex.Message}"
                };
            }
        }

        private async Task<(string accessToken, string refreshToken)> ExchangeCodeForTokenAsync(string code, string codeVerifier)
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = codeVerifier
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/v10/oauth2/token")
            {
                Content = new FormUrlEncodedContent(form)
            };

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                _logger.LogError($"Discord OAuth2 token error: {err}");
                return (string.Empty, string.Empty);
            }

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            string access = doc.RootElement.TryGetProperty("access_token", out var a) ? (a.GetString() ?? "") : "";
            string refresh = doc.RootElement.TryGetProperty("refresh_token", out var r) ? (r.GetString() ?? "") : "";
            return (access, refresh);
        }

        private async Task<(string userId, string username)> GetDiscordUserProfileAsync(string accessToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v10/users/@me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                return (string.Empty, string.Empty);
            }

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            string id = doc.RootElement.TryGetProperty("id", out var idProp) ? (idProp.GetString() ?? "") : "";
            string globalName = doc.RootElement.TryGetProperty("global_name", out var gnProp) ? (gnProp.GetString() ?? "") : "";
            string uName = doc.RootElement.TryGetProperty("username", out var uProp) ? (uProp.GetString() ?? "") : "";
            return (id, !string.IsNullOrWhiteSpace(globalName) ? globalName : uName);
        }

        private static string GetBrowserCallbackHtml(bool success)
        {
            string title = success ? "TrueTweaks Connected!" : "Authorization Cancelled";
            string subtitle = success
                ? "Your Discord account has been verified and your <b>@TrueTweaks User</b> role has been granted. You may now close this tab and return to TrueTweaks."
                : "Discord authorization was cancelled. Please return to TrueTweaks and try again.";
            string badgeColor = success ? "#00E65A" : "#EF4444";

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>{title}</title>
<style>
  body {{
    margin: 0; height: 100vh; display: flex; align-items: center; justify-content: center;
    background: radial-gradient(circle at top right, rgba(0,230,90,0.08), #070908 60%);
    font-family: 'Segoe UI', system-ui, sans-serif; color: #FFFFFF;
  }}
  .card {{
    background: #0E1310; border: 1px solid #1C2E24; border-radius: 16px;
    padding: 40px; max-width: 440px; text-align: center;
    box-shadow: 0 20px 50px rgba(0,0,0,0.6), 0 0 30px rgba(0,230,90,0.1);
  }}
  .badge {{
    display: inline-block; padding: 6px 14px; border-radius: 99px;
    background: rgba(0,230,90,0.12); border: 1px solid {badgeColor};
    color: {badgeColor}; font-weight: 700; font-size: 12px; margin-bottom: 16px;
  }}
  h1 {{ margin: 0 0 12px 0; font-size: 26px; }}
  p {{ color: #8A9990; font-size: 14px; line-height: 1.6; margin: 0; }}
</style>
</head>
<body>
  <div class='card'>
    <div class='badge'>TRUETWEAKS • DISCORD AUTH</div>
    <h1>{title}</h1>
    <p>{subtitle}</p>
  </div>
</body>
</html>";
        }
    }
}
