using System;
using System.Threading.Tasks;

namespace TrueTweaks.Services
{
    public class UpdateCheckResult
    {
        public string CurrentVersion { get; set; } = "1.0.0";
        public string LatestVersion { get; set; } = "1.0.0";
        public bool IsUpdateAvailable { get; set; }
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public interface IUpdateService
    {
        string CurrentVersion { get; }
        Task<UpdateCheckResult> CheckForUpdatesAsync();
        Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null);
        Task<bool> UpdateAllAsync(ITweaksManagerService tweaksManager, ISystemMonitorService monitor, IToastService toastService);
    }
}
