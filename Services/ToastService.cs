using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using TrueTweaks.Models;

namespace TrueTweaks.Services
{
    public class ToastService : IToastService
    {
        public ObservableCollection<ToastNotification> ActiveToasts { get; } = new();

        public void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 3800)
        {
            var toast = new ToastNotification
            {
                Title = title,
                Message = message,
                Type = type
            };

            void AddToast()
            {
                // Limite le nombre de toasts simultanés à 3 pour ne pas encombrer l'écran
                while (ActiveToasts.Count >= 3)
                {
                    ActiveToasts.RemoveAt(0);
                }
                ActiveToasts.Add(toast);
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                AddToast();
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(AddToast);
            }

            // Déclenchement de la suppression automatique après le délai spécifié
            _ = Task.Run(async () =>
            {
                await Task.Delay(durationMs);
                Dismiss(toast);
            });
        }

        public void ShowSuccess(string title, string message, int durationMs = 3800) =>
            Show(title, message, ToastType.Success, durationMs);

        public void ShowInfo(string title, string message, int durationMs = 3800) =>
            Show(title, message, ToastType.Info, durationMs);

        public void ShowWarning(string title, string message, int durationMs = 3800) =>
            Show(title, message, ToastType.Warning, durationMs);

        public void ShowError(string title, string message, int durationMs = 4500) =>
            Show(title, message, ToastType.Error, durationMs);

        public void Dismiss(ToastNotification toast)
        {
            void RemoveToast()
            {
                if (ActiveToasts.Contains(toast))
                {
                    ActiveToasts.Remove(toast);
                }
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                RemoveToast();
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(RemoveToast);
            }
        }
    }
}
