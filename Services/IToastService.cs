using System;
using System.Collections.ObjectModel;
using TrueTweaks.Models;

namespace TrueTweaks.Services
{
    public interface IToastService
    {
        ObservableCollection<ToastNotification> ActiveToasts { get; }
        void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 3800);
        void ShowSuccess(string title, string message, int durationMs = 3800);
        void ShowInfo(string title, string message, int durationMs = 3800);
        void ShowWarning(string title, string message, int durationMs = 3800);
        void ShowError(string title, string message, int durationMs = 4500);
        void Dismiss(ToastNotification toast);
    }
}
