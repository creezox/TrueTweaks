using System.Threading.Tasks;

namespace TrueTweaks.Services
{
    public interface IRestorePointService
    {
        Task<bool> CreateRestorePointAsync(string description = "TrueTweaks Optimisation Backup");
        Task<bool> BackupRegistryHiveAsync(string targetPath);
        Task<bool> IsSystemRestoreEnabledAsync();
    }
}
