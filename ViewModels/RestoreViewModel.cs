using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueTweaks.Services;

namespace TrueTweaks.ViewModels
{
    public partial class RestoreViewModel : ViewModelBase
    {
        public override string Title => "Backup & Restore";

        private readonly IRestorePointService _restoreService;
        private readonly ILoggerService _logger;
        private readonly IToastService _toastService;

        [ObservableProperty]
        private string _restorePointDescription = "TrueTweaks System Safety Point";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _lastBackupPath = string.Empty;

        public RestoreViewModel(IRestorePointService restoreService, ILoggerService logger, IToastService toastService)
        {
            _restoreService = restoreService;
            _logger = logger;
            _toastService = toastService;

            string backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrueTweaks_Backups");
            LastBackupPath = backupFolder;
        }

        [RelayCommand]
        private async Task CreateSystemRestorePointAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            _toastService.ShowInfo("Restore Point", "Creating Windows system restore snapshot...");

            try
            {
                string desc = string.IsNullOrWhiteSpace(RestorePointDescription)
                    ? "TrueTweaks System Safety Point"
                    : RestorePointDescription.Trim();

                bool ok = await _restoreService.CreateRestorePointAsync(desc);
                if (ok)
                {
                    string msg = $"Restore point \"{desc}\" created successfully!";
                    _logger.LogSuccess(msg);
                    _toastService.ShowSuccess("Backup Created", msg);
                }
                else
                {
                    _logger.LogWarning("Failed to create restore point.");
                    _toastService.ShowWarning("Warning", "Could not create restore point. Ensure System Protection on drive C: is enabled.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Restore error: {ex.Message}");
                _toastService.ShowError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task BackupRegistryAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            _toastService.ShowInfo("Registry Backup", "Exporting latency and scheduler hives...");

            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrueTweaks_Backups");
                string file = Path.Combine(folder, $"PriorityControl_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

                bool ok = await _restoreService.BackupRegistryHiveAsync(file);
                if (ok)
                {
                    LastBackupPath = file;
                    string msg = $"Registry backup exported: {Path.GetFileName(file)}";
                    _logger.LogSuccess(msg);
                    _toastService.ShowSuccess("Registry Saved", msg);
                }
                else
                {
                    _toastService.ShowWarning("Backup Failed", "Unable to export registry hives.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Export error: {ex.Message}");
                _toastService.ShowError("Error", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
