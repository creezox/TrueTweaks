using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TrueTweaks.Services;

namespace TrueTweaks.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        public override string Title => "Settings";

        private readonly ILoggerService _logger;
        private readonly IUpdateService _updateService;
        private readonly ITweaksManagerService? _tweaksManager;
        private readonly ISystemMonitorService? _monitor;
        private readonly IToastService _toastService;
        private string _downloadUrl = "https://github.com/creezox/TrueTweaks/releases/latest";
        public const string DiscordInviteUrl = "https://discord.gg/truetweaks";
        public string DiscordUrl => DiscordInviteUrl;

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        private bool _minimizeToTray = true;

        [ObservableProperty]
        private bool _checkUpdatesOnStartup = true;

        [ObservableProperty]
        private string _appVersion = "3.0.0";

        [ObservableProperty]
        private string _updateStatusText = "Your installation is up to date.";

        [ObservableProperty]
        private bool _isCheckingUpdate;

        [ObservableProperty]
        private bool _isUpdatingAll;

        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private string _latestVersion = "3.0.0";

        public SettingsViewModel(
            ILoggerService logger,
            IUpdateService updateService,
            ITweaksManagerService? tweaksManager = null,
            ISystemMonitorService? monitor = null,
            IToastService? toastService = null)
        {
            _logger = logger;
            _updateService = updateService;
            _tweaksManager = tweaksManager;
            _monitor = monitor;
            _toastService = toastService ?? new ToastService();
            _startWithWindows = CheckStartupRegistry();
        }

        partial void OnStartWithWindowsChanged(bool value)
        {
            SetStartupRegistry(value);
        }

        private bool CheckStartupRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("TrueTweaks") != null;
            }
            catch
            {
                return false;
            }
        }

        private void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("TrueTweaks", $"\"{exePath}\"");
                        _logger.LogInfo("Start with Windows enabled.");
                        _toastService.ShowSuccess("Startup Settings", "TrueTweaks will now start with Windows.");
                    }
                }
                else
                {
                    key.DeleteValue("TrueTweaks", false);
                    _logger.LogInfo("Start with Windows disabled.");
                    _toastService.ShowInfo("Startup Settings", "Autostart with Windows disabled.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to update Windows startup: {ex.Message}");
                _toastService.ShowError("Error", ex.Message);
            }
        }

        [RelayCommand]
        public async Task CheckUpdatesAsync()
        {
            if (IsCheckingUpdate) return;

            IsCheckingUpdate = true;
            UpdateStatusText = "Checking online definitions...";

            try
            {
                var result = await _updateService.CheckForUpdatesAsync();
                IsUpdateAvailable = result.IsUpdateAvailable;
                LatestVersion = result.LatestVersion;
                _downloadUrl = result.DownloadUrl;

                if (result.IsUpdateAvailable)
                {
                    UpdateStatusText = $"New version v{result.LatestVersion} available!";
                    _toastService.ShowSuccess("Update Available", $"Version v{result.LatestVersion} is ready to download.");
                }
                else
                {
                    UpdateStatusText = $"TrueTweaks v{AppVersion} is up to date (Stable Channel).";
                }
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        [RelayCommand]
        public async Task UpdateAllAsync()
        {
            if (IsUpdatingAll) return;

            IsUpdatingAll = true;
            try
            {
                if (_tweaksManager != null && _monitor != null)
                {
                    await _updateService.UpdateAllAsync(_tweaksManager, _monitor, _toastService);
                }
                else
                {
                    await CheckUpdatesAsync();
                }
            }
            finally
            {
                IsUpdatingAll = false;
            }
        }

        [RelayCommand]
        private async Task InstallUpdateAsync()
        {
            await _updateService.DownloadAndInstallUpdateAsync(_downloadUrl);
        }

        [RelayCommand]
        private void OpenLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to open link: {ex.Message}");
            }
        }
    }
}
