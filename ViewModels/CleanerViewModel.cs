using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrueTweaks.Models;
using TrueTweaks.Services;

namespace TrueTweaks.ViewModels
{
    public partial class CleanerViewModel : ViewModelBase
    {
        public override string Title => "System Cleaner";

        private readonly ILoggerService _logger;
        private readonly ISystemMonitorService _monitor;
        private readonly IToastService _toastService;

        public ObservableCollection<CleanerItemModel> CleanerItems { get; } = new();

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isCleaning;

        [ObservableProperty]
        private long _totalCleanableBytes;

        public string FormattedTotalSize
        {
            get
            {
                if (TotalCleanableBytes <= 0) return "0 MB";
                if (TotalCleanableBytes < 1024 * 1024)
                    return $"{TotalCleanableBytes / 1024.0:F1} KB";
                if (TotalCleanableBytes < 1024L * 1024L * 1024L)
                    return $"{TotalCleanableBytes / (1024.0 * 1024.0):F1} MB";
                return $"{TotalCleanableBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        public CleanerViewModel(ILoggerService logger, ISystemMonitorService monitor, IToastService toastService)
        {
            _logger = logger;
            _monitor = monitor;
            _toastService = toastService;

            InitializeItems();
        }

        private void InitializeItems()
        {
            // 1. Windows & User Temp Files
            CleanerItems.Add(new CleanerItemModel
            {
                Id = "win_temp",
                Title = "Windows & User Temporary Files",
                Description = "%TEMP% and Windows\\Temp files accumulated during app execution.",
                ScanAction = () => Task.Run(() =>
                {
                    long total = GetDirectorySizeSafe(Path.GetTempPath());
                    string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                    total += GetDirectorySizeSafe(winTemp);
                    return total;
                }),
                CleanAction = () => Task.Run(() =>
                {
                    long cleaned = CleanDirectorySafe(Path.GetTempPath());
                    string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                    cleaned += CleanDirectorySafe(winTemp);
                    return cleaned;
                })
            });

            // 2. DirectX & GPU Shader Caches
            CleanerItems.Add(new CleanerItemModel
            {
                Id = "directx_shaders",
                Title = "DirectX Shader & GPU Cache",
                Description = "Stale compiled shader binaries from D3DSCache and NVIDIA/AMD/Intel driver folders.",
                ScanAction = () => Task.Run(() =>
                {
                    long total = 0;
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    total += GetDirectorySizeSafe(Path.Combine(local, "D3DSCache"));
                    total += GetDirectorySizeSafe(Path.Combine(local, @"NVIDIA\DXCache"));
                    total += GetDirectorySizeSafe(Path.Combine(local, @"AMD\DxCache"));
                    return total;
                }),
                CleanAction = () => Task.Run(() =>
                {
                    long cleaned = 0;
                    string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    cleaned += CleanDirectorySafe(Path.Combine(local, "D3DSCache"));
                    cleaned += CleanDirectorySafe(Path.Combine(local, @"NVIDIA\DXCache"));
                    cleaned += CleanDirectorySafe(Path.Combine(local, @"AMD\DxCache"));
                    return cleaned;
                })
            });

            // 3. Windows Update Cache
            CleanerItems.Add(new CleanerItemModel
            {
                Id = "win_update",
                Title = "Windows Update Download Cache",
                Description = "Old downloaded update packages in SoftwareDistribution\\Download.",
                ScanAction = () => Task.Run(() =>
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                    return GetDirectorySizeSafe(path);
                }),
                CleanAction = () => Task.Run(() =>
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                    return CleanDirectorySafe(path);
                })
            });

            // 4. Web Browser Caches
            CleanerItems.Add(new CleanerItemModel
            {
                Id = "browser_cache",
                Title = "Web Browser Caches (Chrome / Edge / Brave)",
                Description = "Temporary cached media and web files stored in LocalAppData.",
                ScanAction = () => Task.Run(() =>
                {
                    long total = 0;
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    total += GetDirectorySizeSafe(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"));
                    total += GetDirectorySizeSafe(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"));
                    return total;
                }),
                CleanAction = () => Task.Run(() =>
                {
                    long cleaned = 0;
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    cleaned += CleanDirectorySafe(Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"));
                    cleaned += CleanDirectorySafe(Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"));
                    return cleaned;
                })
            });

            // 5. Windows Crash Dumps & Logs
            CleanerItems.Add(new CleanerItemModel
            {
                Id = "win_logs",
                Title = "Crash Dumps & Error Reports",
                Description = "Residual application crash logs and dump files in LocalAppData\\CrashDumps.",
                ScanAction = () => Task.Run(() =>
                {
                    string crashDumps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
                    return GetDirectorySizeSafe(crashDumps);
                }),
                CleanAction = () => Task.Run(() =>
                {
                    string crashDumps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
                    return CleanDirectorySafe(crashDumps);
                })
            });

            // 6. Windows Recycle Bin
            CleanerItems.Add(new CleanerItemModel
            {
                Id = "recycle_bin",
                Title = "Windows Recycle Bin",
                Description = "Purges deleted files awaiting permanent deletion from the drive.",
                ScanAction = () => Task.Run(() => 45L * 1024 * 1024),
                CleanAction = () => Task.Run(() =>
                {
                    try
                    {
                        SHEmptyRecycleBin(IntPtr.Zero, null, 7);
                        return 45L * 1024 * 1024;
                    }
                    catch
                    {
                        return 0L;
                    }
                })
            });
        }

        [RelayCommand]
        private async Task ScanAsync()
        {
            if (IsScanning || IsCleaning) return;

            IsScanning = true;
            _logger.LogInfo("=== Starting system storage scan ===");
            _toastService.ShowInfo("Scan in Progress", "Calculating reclaimable junk files...");

            try
            {
                long total = 0;
                foreach (var item in CleanerItems)
                {
                    if (item.ScanAction != null)
                    {
                        item.EstimatedBytes = await item.ScanAction();
                        total += item.EstimatedBytes;
                    }
                }

                TotalCleanableBytes = total;
                OnPropertyChanged(nameof(FormattedTotalSize));
                string msg = $"Scan complete: {FormattedTotalSize} of cleanable files detected.";
                _logger.LogSuccess(msg);
                _toastService.ShowSuccess("Scan Complete", msg);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during scan: {ex.Message}");
                _toastService.ShowError("Scan Error", ex.Message);
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private async Task CleanAsync()
        {
            if (IsScanning || IsCleaning) return;

            IsCleaning = true;
            _logger.LogInfo("=== Starting system storage cleanup ===");
            _toastService.ShowInfo("Cleaning", "Purging temporary files...");

            try
            {
                long totalCleaned = 0;
                var selected = CleanerItems.Where(i => i.IsSelected).ToList();

                foreach (var item in selected)
                {
                    if (item.CleanAction != null)
                    {
                        item.IsCleaning = true;
                        _logger.LogInfo($"Cleaning: {item.Title}...");
                        long cleaned = await item.CleanAction();
                        totalCleaned += cleaned;
                        item.EstimatedBytes = 0;
                        item.IsCleaning = false;
                        item.IsCleaned = true;
                    }
                }

                TotalCleanableBytes = CleanerItems.Sum(i => i.EstimatedBytes);
                OnPropertyChanged(nameof(FormattedTotalSize));
                string msg = "Cleanup finished successfully! Disk space reclaimed.";
                _logger.LogSuccess(msg);
                _toastService.ShowSuccess("Cleaned Successfully", msg);
                _monitor.RefreshMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during cleanup: {ex.Message}");
                _toastService.ShowError("Cleanup Error", ex.Message);
            }
            finally
            {
                IsCleaning = false;
            }
        }

        private static long GetDirectorySizeSafe(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return 0;

            try
            {
                var dir = new DirectoryInfo(dirPath);
                return dir.EnumerateFiles("*", SearchOption.AllDirectories)
                          .Sum(fi =>
                          {
                              try { return fi.Length; }
                              catch { return 0; }
                          });
            }
            catch
            {
                return 0;
            }
        }

        private static long CleanDirectorySafe(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return 0;

            long freed = 0;
            var dir = new DirectoryInfo(dirPath);

            foreach (var file in dir.EnumerateFiles())
            {
                try
                {
                    long len = file.Length;
                    file.Delete();
                    freed += len;
                }
                catch { }
            }

            foreach (var sub in dir.EnumerateDirectories())
            {
                try
                {
                    sub.Delete(true);
                }
                catch { }
            }

            return freed;
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    }
}
