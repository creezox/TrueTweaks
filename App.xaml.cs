using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TrueTweaks.Services;
using TrueTweaks.ViewModels;

namespace TrueTweaks
{
    public partial class App : Application
    {
        private static ILoggerService? _globalLogger;

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                // Initialisation des services métiers fondamentaux
                var logger = new LoggerService();
                _globalLogger = logger;

                var monitor = new SystemMonitorService(logger);
                var restoreService = new RestorePointService(logger);
                var tweaksManager = new TweaksManagerService(logger, monitor);
                var updateService = new UpdateService(logger);
                var toastService = new ToastService();

                // Construction du MainViewModel d'orchestration
                var mainVm = new MainViewModel(logger, monitor, tweaksManager, restoreService, updateService, toastService);

                // Création et affichage de la fenêtre principale
                var mainWindow = new MainWindow
                {
                    DataContext = mainVm
                };

                mainWindow.Show();

                // Initialisation asynchrone des optimisations et vérification du statut actuel
                await mainVm.InitializeAsync();
            }
            catch (Exception ex)
            {
                LogCrashToFile(ex);
                MessageBox.Show($"Erreur d'initialisation True Tweaks :\n{ex.Message}", "True Tweaks - Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _globalLogger?.LogError($"Erreur d'interface interceptée : {e.Exception.Message}");
            LogCrashToFile(e.Exception);
            e.Handled = true; // Empêche tout crash fatal "Unknown Hard Error"
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                _globalLogger?.LogError($"Erreur critique interceptée : {ex.Message}");
                LogCrashToFile(ex);
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _globalLogger?.LogWarning($"Exception de tâche en arrière-plan : {e.Exception.Message}");
            e.SetObserved();
        }

        private static void LogCrashToFile(Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueTweaks");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }
    }
}
