using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace TrueTweaks.Services
{
    public class InstallerService
    {
        private readonly ILoggerService _logger;
        public const string RegistryKeyPath = @"Software\TrueTweaks";
        public static string DefaultInstallDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TrueTweaks");

        public InstallerService(ILoggerService logger)
        {
            _logger = logger;
        }

        public bool IsInstalled()
        {
            try
            {
                string currentExe = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName
                    ?? string.Empty;

                // If running directly from Program Files\TrueTweaks, mark installed and return true
                if (!string.IsNullOrEmpty(currentExe) &&
                    currentExe.StartsWith(DefaultInstallDir, StringComparison.OrdinalIgnoreCase))
                {
                    SetInstalledFlag(true);
                    return true;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                if (key?.GetValue("IsInstalled") is int val && val == 1)
                {
                    string installedExe = Path.Combine(DefaultInstallDir, "TrueTweaks.exe");
                    if (File.Exists(installedExe))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore
            }
            return false;
        }

        public void SetInstalledFlag(bool installed)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath, true);
                key?.SetValue("IsInstalled", installed ? 1 : 0, RegistryValueKind.DWord);
                key?.SetValue("InstallPath", DefaultInstallDir, RegistryValueKind.String);
            }
            catch
            {
                // Ignore
            }
        }

        public async Task<bool> InstallApplicationAsync(
            bool createDesktopShortcut,
            bool createStartMenuShortcut,
            IProgress<(double percent, string status)>? progress = null)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    progress?.Report((15, "Creating installation directory in Program Files..."));
                    await Task.Delay(250);

                    if (!Directory.Exists(DefaultInstallDir))
                    {
                        Directory.CreateDirectory(DefaultInstallDir);
                    }

                    string currentExe = Environment.ProcessPath
                        ?? Process.GetCurrentProcess().MainModule?.FileName
                        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrueTweaks.exe");

                    string targetExe = Path.Combine(DefaultInstallDir, "TrueTweaks.exe");
                    string targetIco = Path.Combine(DefaultInstallDir, "tweaks.ico");

                    progress?.Report((40, "Copying TrueTweaks engine binary (win-x64)..."));
                    await Task.Delay(300);

                    if (!string.Equals(currentExe, targetExe, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(currentExe, targetExe, true);
                    }

                    // Extract embedded tweaks.ico to install dir for shortcuts & Control Panel
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/tweaks.ico"));
                            if (streamInfo != null)
                            {
                                using var fs = new FileStream(targetIco, FileMode.Create, FileAccess.Write);
                                streamInfo.Stream.CopyTo(fs);
                            }
                        });
                    }
                    catch
                    {
                        // Fallback icon is targetExe itself
                    }

                    progress?.Report((70, "Creating Windows Desktop & Start Menu shortcuts..."));
                    await Task.Delay(250);

                    if (createDesktopShortcut)
                    {
                        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        CreateShortcut(Path.Combine(desktopDir, "TrueTweaks.lnk"), targetExe, DefaultInstallDir, targetIco);
                    }

                    if (createStartMenuShortcut)
                    {
                        string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TrueTweaks");
                        if (!Directory.Exists(startMenuDir))
                        {
                            Directory.CreateDirectory(startMenuDir);
                        }
                        CreateShortcut(Path.Combine(startMenuDir, "TrueTweaks.lnk"), targetExe, DefaultInstallDir, targetIco);
                    }

                    progress?.Report((90, "Registering TrueTweaks in Windows Installed Apps..."));
                    await Task.Delay(200);

                    RegisterInAddRemovePrograms(targetExe, targetIco);
                    SetInstalledFlag(true);

                    progress?.Report((100, "TrueTweaks installed successfully!"));
                    _logger.LogSuccess($"TrueTweaks installed to {DefaultInstallDir}");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Installer error: {ex.Message}");
                    progress?.Report((0, $"Installation error: {ex.Message}"));
                    return false;
                }
            });
        }

        private void CreateShortcut(string shortcutPath, string targetExe, string workingDir, string iconPath)
        {
            try
            {
                string ico = File.Exists(iconPath) ? iconPath : targetExe;
                string psScript = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$Shortcut.TargetPath = '{targetExe.Replace("'", "''")}'
$Shortcut.WorkingDirectory = '{workingDir.Replace("'", "''")}'
$Shortcut.IconLocation = '{ico.Replace("'", "''")}'
$Shortcut.Description = 'TrueTweaks — Windows Kernel, Latency & FPS Optimization Engine'
$Shortcut.Save()
";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{psScript.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(4000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Shortcut creation warning: {ex.Message}");
            }
        }

        private void RegisterInAddRemovePrograms(string targetExe, string iconPath)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TrueTweaks", true);
                if (key != null)
                {
                    key.SetValue("DisplayName", "TrueTweaks", RegistryValueKind.String);
                    key.SetValue("DisplayVersion", "1.0.0", RegistryValueKind.String);
                    key.SetValue("Publisher", "TrueTweaks", RegistryValueKind.String);
                    key.SetValue("DisplayIcon", File.Exists(iconPath) ? iconPath : targetExe, RegistryValueKind.String);
                    key.SetValue("InstallLocation", DefaultInstallDir, RegistryValueKind.String);
                    key.SetValue("URLInfoAbout", "https://github.com/creezox/TrueTweaks", RegistryValueKind.String);
                    key.SetValue("HelpLink", "https://discord.gg/truetweaks", RegistryValueKind.String);
                    key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{DefaultInstallDir}\"", RegistryValueKind.String);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch
            {
                // Ignore if HKLM uninstall registration fails
            }
        }
    }
}
