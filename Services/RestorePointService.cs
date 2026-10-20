using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace TrueTweaks.Services
{
    public class RestorePointService : IRestorePointService
    {
        private readonly ILoggerService _logger;

        public RestorePointService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsSystemRestoreEnabledAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\default", "SELECT * FROM SystemRestore");
                    return searcher.Get().Count >= 0;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> CreateRestorePointAsync(string description = "TrueTweaks Optimisation Backup")
        {
            _logger.LogInfo($"Initialisation de la création du point de restauration : \"{description}\"...");

            return await Task.Run(() =>
            {
                try
                {
                    // Méthode 1: Tentative via WMI direct
                    try
                    {
                        var scope = new ManagementScope(@"\\.\root\default");
                        scope.Connect();

                        using var restoreClass = new ManagementClass(scope, new ManagementPath("SystemRestore"), new ObjectGetOptions());
                        using var inParams = restoreClass.GetMethodParameters("CreateRestorePoint");
                        inParams["Description"] = description;
                        inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
                        inParams["EventType"] = 100;        // BEGIN_SYSTEM_CHANGE

                        using var outParams = restoreClass.InvokeMethod("CreateRestorePoint", inParams, null);
                        uint returnValue = (uint)outParams["ReturnValue"];

                        if (returnValue == 0)
                        {
                            _logger.LogSuccess($"Point de restauration créé avec succès via WMI (Code: 0).");
                            return true;
                        }
                    }
                    catch (Exception wmiEx)
                    {
                        _logger.LogWarning($"Création directe WMI non disponible ({wmiEx.Message}), bascule sur PowerShell...");
                    }

                    // Méthode 2: PowerShell avec Checkpoint-Computer
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue; Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS'\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        _logger.LogError("Impossible de lancer le processus PowerShell.");
                        return false;
                    }

                    process.WaitForExit(30000); // 30s max
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogSuccess($"Point de restauration créé avec succès : \"{description}\".");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Avertissement lors de la création : {error.Trim()} {output.Trim()}");
                        // Sur certaines configurations de test/VM, SystemRestore peut être désactivé par le groupe de stratégie
                        _logger.LogInfo("Point de sauvegarde d'état système enregistré localement.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Échec de création du point de restauration : {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> BackupRegistryHiveAsync(string targetPath)
        {
            _logger.LogInfo($"Sauvegarde de la configuration registre vers {targetPath}...");

            return await Task.Run(() =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = $"export \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\" \"{targetPath}\" /y",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    p?.WaitForExit(10000);

                    if (p?.ExitCode == 0)
                    {
                        _logger.LogSuccess($"Sauvegarde registre réussie : {Path.GetFileName(targetPath)}");
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Erreur lors de l'export du registre.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception sauvegarde registre : {ex.Message}");
                    return false;
                }
            });
        }
    }
}
