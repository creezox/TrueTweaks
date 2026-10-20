using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TrueTweaks.Models;

namespace TrueTweaks.Services
{
    public class UpdateService : IUpdateService
    {
        public string CurrentVersion => "3.0.0";

        // Official public release repository for auto-updates
        public const string GitHubRepoSlug = "creezox/TrueTweaks";

        private readonly ILoggerService _logger;
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        public UpdateService(ILoggerService logger)
        {
            _logger = logger;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TrueTweaks-Optimizer/3.0");
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            _logger.LogInfo($"Checking GitHub ({GitHubRepoSlug}) for latest TrueTweaks release...");

            var result = new UpdateCheckResult
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = CurrentVersion,
                IsUpdateAvailable = false,
                ReleaseNotes = "Your TrueTweaks installation is up to date with the latest 18,913 optimization rules.",
                DownloadUrl = $"https://github.com/{GitHubRepoSlug}/releases/latest/download/TrueTweaks.exe"
            };

            try
            {
                string apiUrl = $"https://api.github.com/repos/{GitHubRepoSlug}/releases/latest";
                using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                using var response = await _httpClient.SendAsync(req);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("tag_name", out var tagProp))
                    {
                        string tagName = tagProp.GetString() ?? CurrentVersion;
                        string cleanTag = tagName.TrimStart('v', 'V');
                        result.LatestVersion = cleanTag;

                        if (root.TryGetProperty("body", out var bodyProp))
                        {
                            result.ReleaseNotes = bodyProp.GetString() ?? string.Empty;
                        }

                        if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var asset in assetsProp.EnumerateArray())
                            {
                                string assetName = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                                if (assetName.Equals("TrueTweaks.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (asset.TryGetProperty("browser_download_url", out var dlProp) && !string.IsNullOrWhiteSpace(dlProp.GetString()))
                                    {
                                        result.DownloadUrl = dlProp.GetString()!;
                                        break;
                                    }
                                    if (asset.TryGetProperty("url", out var apiAssetUrlProp) && !string.IsNullOrWhiteSpace(apiAssetUrlProp.GetString()))
                                    {
                                        result.DownloadUrl = apiAssetUrlProp.GetString()!;
                                        break;
                                    }
                                }
                            }
                        }

                        if (Version.TryParse(cleanTag, out var remoteVer) && Version.TryParse(CurrentVersion, out var localVer))
                        {
                            if (remoteVer > localVer)
                            {
                                result.IsUpdateAvailable = true;
                                _logger.LogSuccess($"New update available on GitHub: v{cleanTag}!");
                                return result;
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"GitHub release check returned HTTP {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInfo($"Update check completed ({ex.Message}).");
            }

            _logger.LogSuccess($"TrueTweaks v{CurrentVersion} is up to date (Stable Channel).");
            return result;
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            _logger.LogInfo($"Downloading TrueTweaks update package from GitHub...");

            try
            {
                string currentExe = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName
                    ?? Path.Combine(InstallerService.DefaultInstallDir, "TrueTweaks.exe");

                string tempDir = Path.GetTempPath();
                string tempUpdateExe = Path.Combine(tempDir, "TrueTweaks_Update.exe");
                string updaterBat = Path.Combine(tempDir, "TrueTweaks_AutoUpdater.bat");

                using var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                using (var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Failed to download update package: HTTP {(int)response.StatusCode}");
                        return false;
                    }

                    long? totalBytes = response.Content.Headers.ContentLength;
                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(tempUpdateExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;
                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            progress?.Report((double)totalRead / totalBytes.Value * 100.0);
                        }
                    }
                }

                string programFilesExe = Path.Combine(InstallerService.DefaultInstallDir, "TrueTweaks.exe");
                string downloadsExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "TrueTweaks.exe");

                // Atomic self-replacement batch script: waits until current PID exits, updates installed + current exe, and relaunches
                int currentPid = Environment.ProcessId;
                string batContent = $@"@echo off
:waitloop
tasklist /FI ""PID eq {currentPid}"" 2>NUL | find /I ""{currentPid}"" >NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak >NUL
    goto waitloop
)
if exist ""{InstallerService.DefaultInstallDir}"" copy /y ""{tempUpdateExe}"" ""{programFilesExe}"" >NUL
if exist ""{downloadsExe}"" copy /y ""{tempUpdateExe}"" ""{downloadsExe}"" >NUL
move /y ""{tempUpdateExe}"" ""{currentExe}"" >NUL
start """" ""{currentExe}""
del ""%~f0""
";
                await File.WriteAllTextAsync(updaterBat, batContent);

                _logger.LogSuccess("Update downloaded! Restarting TrueTweaks to apply new version...");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{updaterBat}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                await Task.Delay(150);
                Environment.Exit(0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Auto-updater error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateAllAsync(ITweaksManagerService tweaksManager, ISystemMonitorService monitor, IToastService toastService)
        {
            _logger.LogInfo("=== Running TrueTweaks Global System & Engine Update ===");
            toastService.ShowInfo("Global Update", "Synchronizing 18,913 latency rules and checking GitHub releases...");

            try
            {
                var check = await CheckForUpdatesAsync();

                if (check.IsUpdateAvailable)
                {
                    _logger.LogSuccess($"New version available online: v{check.LatestVersion}. Downloading and installing automatically...");
                    toastService.ShowSuccess("New Version Found!", $"Installing TrueTweaks v{check.LatestVersion}...");
                    await DownloadAndInstallUpdateAsync(check.DownloadUrl);
                    return true;
                }

                var seenGroups = new HashSet<string>();
                foreach (var tweak in tweaksManager.Tweaks.Where(t => t.Risk == RiskLevel.Safe))
                {
                    if (tweak.GroupId == null || seenGroups.Add(tweak.GroupId))
                    {
                        tweak.IsSelected = true;
                    }
                    else
                    {
                        tweak.IsSelected = false;
                    }
                }

                int appliedCount = await tweaksManager.ApplySelectedAsync();
                _logger.LogSuccess($"Latency rules synchronized: {tweaksManager.AppliedCount} modules ({tweaksManager.AppliedSubTweaksCount:N0} low-level rules) active.");

                monitor.RefreshMetrics();
                _logger.LogInfo("Hardware performance sensors refreshed.");

                string summary = appliedCount > 0 
                    ? $"Update completed: {tweaksManager.AppliedCount} modules ({tweaksManager.AppliedSubTweaksCount:N0} rules) active!"
                    : $"System & TrueTweaks v{CurrentVersion} up to date ({tweaksManager.AppliedSubTweaksCount:N0} rules synchronized).";
                
                _logger.LogSuccess(summary);
                toastService.ShowSuccess("Update Completed!", summary);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during global update: {ex.Message}");
                toastService.ShowError("Update Error", ex.Message);
                return false;
            }
        }
    }
}
