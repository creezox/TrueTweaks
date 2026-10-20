using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueTweaks.Models;
using TrueTweaks.Services;

namespace TrueTweaks.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public ILoggerService Logger { get; }
        public ISystemMonitorService Monitor { get; }
        public ITweaksManagerService TweaksManager { get; }
        public IRestorePointService RestoreService { get; }
        public IUpdateService UpdateService { get; }
        public IToastService ToastService { get; }
        public InstallerService Installer { get; }
        public DiscordAuthService DiscordAuth { get; }

        public DashboardViewModel DashboardVM { get; }
        public TweaksViewModel TweaksVM { get; }
        public CleanerViewModel CleanerVM { get; }
        public RestoreViewModel RestoreVM { get; }
        public SettingsViewModel SettingsVM { get; }

        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private string _activeViewKey = "DASHBOARD";

        [ObservableProperty]
        private bool _isConsoleExpanded = true;

        [ObservableProperty]
        private WindowState _windowState = WindowState.Normal;

        [ObservableProperty]
        private bool _isUpdatingAll;

        // ================= 1. PC INSTALLER WIZARD STATE =================
        [ObservableProperty]
        private bool _isInstallerVisible;

        [ObservableProperty]
        private bool _createDesktopShortcut = true;

        [ObservableProperty]
        private bool _createStartMenuShortcut = true;

        [ObservableProperty]
        private bool _isInstalling;

        [ObservableProperty]
        private double _installProgress;

        [ObservableProperty]
        private string _installStatusText = string.Empty;

        public string InstallLocationDisplay => InstallerService.DefaultInstallDir;

        // ================= 2. DISCORD AUTH & ROLE GATE STATE =================
        [ObservableProperty]
        private bool _isDiscordGateVisible;

        [ObservableProperty]
        private bool _isDiscordVerified;

        [ObservableProperty]
        private bool _isDiscordAuthenticating;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(DisplayInitial))]
        private string _discordUsername = string.Empty;

        /// <summary>Name shown in "Welcome back, [user]" and the sidebar profile card.</summary>
        public string DisplayName => !string.IsNullOrWhiteSpace(DiscordUsername) ? DiscordUsername : Environment.UserName;

        public string DisplayInitial => string.IsNullOrWhiteSpace(DisplayName) ? "T" : DisplayName.Substring(0, 1).ToUpperInvariant();

        public string AccountPlanText => IsDiscordVerified ? "Discord Verified" : "Free";

        partial void OnIsDiscordVerifiedChanged(bool value) => OnPropertyChanged(nameof(AccountPlanText));

        [ObservableProperty]
        private string _discordUserIdInput = string.Empty;

        [ObservableProperty]
        private string _discordStatusText = string.Empty;

        public string ConsoleToggleButtonText => IsConsoleExpanded ? "Terminal [▼ Hide]" : "Terminal [▲ Expand]";

        public MainViewModel(
            ILoggerService logger,
            ISystemMonitorService monitor,
            ITweaksManagerService tweaksManager,
            IRestorePointService restoreService,
            IUpdateService? updateService = null,
            IToastService? toastService = null)
        {
            Logger = logger;
            Monitor = monitor;
            TweaksManager = tweaksManager;
            RestoreService = restoreService;
            ToastService = toastService ?? new ToastService();
            UpdateService = updateService ?? new UpdateService(logger);
            Installer = new InstallerService(logger);
            DiscordAuth = new DiscordAuthService(logger);

            DashboardVM = new DashboardViewModel(Monitor, TweaksManager, RestoreService, Logger, ToastService, UpdateService);
            TweaksVM = new TweaksViewModel(TweaksManager, Logger, ToastService);
            CleanerVM = new CleanerViewModel(Logger, Monitor, ToastService);
            RestoreVM = new RestoreViewModel(RestoreService, Logger, ToastService);
            SettingsVM = new SettingsViewModel(Logger, UpdateService, TweaksManager, Monitor, ToastService);

            // Initial view
            _currentView = DashboardVM;
            _activeViewKey = "DASHBOARD";

            // Check installation & Discord verification states
            bool installed = Installer.IsInstalled();
            bool discordSaved = DiscordAuth.TryLoadSavedSession(out string savedUserId, out string savedUsername);

            if (discordSaved)
            {
                _isDiscordVerified = true;
                _discordUsername = savedUsername;
                _discordUserIdInput = savedUserId;
            }

            if (!installed)
            {
                _isInstallerVisible = true;
                _isDiscordGateVisible = false;
            }
            else if (!_isDiscordVerified)
            {
                _isInstallerVisible = false;
                _isDiscordGateVisible = true;
            }

            // Start hardware monitor
            Monitor.Start();

            Logger.LogInfo("Initializing TrueTweaks low-latency kernel engine...");
            Logger.LogSuccess("Hardware telemetry probes and optimization services online.");
        }

        partial void OnIsConsoleExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ConsoleToggleButtonText));
        }

        public async Task InitializeAsync()
        {
            await TweaksManager.InitializeTweaksAsync();

            // Re-verify Discord membership & ensure role is assigned in background if already logged in
            if (IsDiscordVerified && !string.IsNullOrWhiteSpace(DiscordUserIdInput) && !string.IsNullOrWhiteSpace(DiscordAuthService.GuildId))
            {
                _ = Task.Run(async () =>
                {
                    var verify = await DiscordAuth.VerifyMembershipAndAssignRoleAsync(DiscordUserIdInput, DiscordUsername);
                    if (!verify.Success)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            IsDiscordVerified = false;
                            IsDiscordGateVisible = !IsInstallerVisible;
                            DiscordStatusText = verify.StatusMessage;
                        });
                    }
                });
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                await SettingsVM.CheckUpdatesAsync();
            });
        }

        // ================= INSTALLER COMMANDS =================
        [RelayCommand]
        private async Task InstallToPcAsync()
        {
            if (IsInstalling) return;
            IsInstalling = true;

            var progress = new Progress<(double percent, string status)>(p =>
            {
                InstallProgress = p.percent;
                InstallStatusText = p.status;
            });

            bool ok = await Installer.InstallApplicationAsync(CreateDesktopShortcut, CreateStartMenuShortcut, progress);
            IsInstalling = false;

            if (ok)
            {
                ToastService.ShowSuccess("Installation Complete", "TrueTweaks has been installed on your PC with Desktop & Start Menu shortcuts.");
                await Task.Delay(500);
                IsInstallerVisible = false;
                if (!IsDiscordVerified)
                {
                    IsDiscordGateVisible = true;
                }
            }
            else
            {
                ToastService.ShowError("Installer Error", InstallStatusText);
            }
        }

        [RelayCommand]
        private void SkipInstaller()
        {
            Installer.SetInstalledFlag(true);
            IsInstallerVisible = false;
            if (!IsDiscordVerified)
            {
                IsDiscordGateVisible = true;
            }
        }

        // ================= DISCORD AUTH & ROLE COMMANDS =================
        [RelayCommand]
        private void JoinDiscordServer()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = DiscordAuthService.DiscordInviteUrl,
                    UseShellExecute = true
                });
                DiscordStatusText = "Discord invite opened! Once you join the server, click 'Connect with Discord' (or verify your Discord ID below) to claim your @TrueTweaks User role.";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open Discord invite: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task LoginWithDiscordAsync()
        {
            if (IsDiscordAuthenticating) return;
            IsDiscordAuthenticating = true;

            try
            {
                var progress = new Progress<string>(msg => DiscordStatusText = msg);

                // Run full browser OAuth2 PKCE flow
                if (!string.IsNullOrWhiteSpace(DiscordAuthService.ClientId))
                {
                    var result = await DiscordAuth.AuthenticateWithDiscordBrowserAsync(progress);
                    DiscordStatusText = result.StatusMessage;

                    if (result.Success)
                    {
                        DiscordUsername = result.Username;
                        DiscordUserIdInput = result.UserId;
                        IsDiscordVerified = true;
                        IsDiscordGateVisible = false;
                        ToastService.ShowSuccess("Discord Verified!", $"Welcome @{result.Username}! Your @TrueTweaks User role is active.");
                    }
                    else if (!string.IsNullOrWhiteSpace(result.UserId))
                    {
                        DiscordUserIdInput = result.UserId;
                        DiscordUsername = result.Username;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(DiscordUserIdInput))
                {
                    await VerifyDiscordByIdAsync();
                }
                else
                {
                    DiscordStatusText = "Please join the Discord server first and enter your Discord User ID (or click Connect once OAuth2 is active).";
                }
            }
            finally
            {
                IsDiscordAuthenticating = false;
            }
        }

        [RelayCommand]
        private async Task VerifyDiscordByIdAsync()
        {
            if (IsDiscordAuthenticating) return;

            string cleanId = DiscordUserIdInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cleanId))
            {
                DiscordStatusText = "Please enter your Discord User ID or click 'Connect with Discord'.";
                return;
            }

            IsDiscordAuthenticating = true;
            DiscordStatusText = "Checking TrueTweaks Discord server membership & assigning @TrueTweaks User role...";

            try
            {
                var result = await DiscordAuth.VerifyMembershipAndAssignRoleAsync(cleanId, DiscordUsername);
                DiscordStatusText = result.StatusMessage;

                if (result.Success)
                {
                    DiscordUsername = result.Username;
                    IsDiscordVerified = true;
                    IsDiscordGateVisible = false;
                    ToastService.ShowSuccess("Discord Verified!", $"Welcome @{result.Username}! Your @TrueTweaks User role is active.");
                }
            }
            finally
            {
                IsDiscordAuthenticating = false;
            }
        }

        [RelayCommand]
        public async Task UpdateAllSystemAsync()
        {
            if (IsUpdatingAll) return;

            IsUpdatingAll = true;
            try
            {
                await UpdateService.UpdateAllAsync(TweaksManager, Monitor, ToastService);
            }
            finally
            {
                IsUpdatingAll = false;
            }
        }

        [RelayCommand]
        private void Navigate(string viewKey)
        {
            ActiveViewKey = viewKey;

            // Sidebar optimization sections: "OPT:<CATEGORY>" opens the Optimization Center filtered on that category
            if (viewKey.StartsWith("OPT:", StringComparison.OrdinalIgnoreCase))
            {
                TweaksVM.SearchText = string.Empty;
                TweaksVM.SelectedCategory = viewKey.Substring(4);
                CurrentView = TweaksVM;
                Logger.LogInfo($"Navigated to: {TweaksVM.CategoryTitle}");
                return;
            }

            CurrentView = viewKey switch
            {
                "DASHBOARD" => DashboardVM,
                "TWEAKS" => TweaksVM,
                "CLEANER" => CleanerVM,
                "RESTORE" => RestoreVM,
                "SETTINGS" => SettingsVM,
                _ => DashboardVM
            };

            Logger.LogInfo($"Navigated to: {CurrentView.Title}");
        }

        [RelayCommand]
        private void ToggleConsole()
        {
            IsConsoleExpanded = !IsConsoleExpanded;
        }

        [RelayCommand]
        private void ClearLogs()
        {
            Logger.Clear();
            Logger.LogInfo("Execution console reset.");
            ToastService.ShowInfo("Console", "Execution log cleared.");
        }

        [RelayCommand]
        private void DismissToast(ToastNotification? toast)
        {
            if (toast != null)
            {
                ToastService.Dismiss(toast);
            }
        }

        [RelayCommand]
        private void MinimizeWindow(Window? window)
        {
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        [RelayCommand]
        private void MaximizeRestoreWindow(Window? window)
        {
            if (window != null)
            {
                window.WindowState = window.WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }
        }

        [RelayCommand]
        private void CloseWindow(Window? window)
        {
            window?.Close();
        }

        [RelayCommand]
        private void OpenDiscord()
        {
            SettingsVM.OpenLinkCommand.Execute(SettingsViewModel.DiscordInviteUrl);
        }
    }
}
