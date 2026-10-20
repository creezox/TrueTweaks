using System;
using System.Windows.Media;

namespace TrueTweaks.Models
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class ToastNotification
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; } = ToastType.Info;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string Icon => Type switch
        {
            ToastType.Success => "✔",
            ToastType.Warning => "⚠",
            ToastType.Error => "✖",
            _ => "ℹ"
        };

        public SolidColorBrush AccentBrush => Type switch
        {
            ToastType.Success => new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x5A)),
            ToastType.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x20)),
            ToastType.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x4D)),
            _ => new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8))
        };

        public SolidColorBrush BackgroundBrush => Type switch
        {
            ToastType.Success => new SolidColorBrush(Color.FromArgb(0xEA, 0x0A, 0x14, 0x0E)),
            ToastType.Warning => new SolidColorBrush(Color.FromArgb(0xEA, 0x1A, 0x13, 0x08)),
            ToastType.Error => new SolidColorBrush(Color.FromArgb(0xEA, 0x1A, 0x0A, 0x0A)),
            _ => new SolidColorBrush(Color.FromArgb(0xEA, 0x0B, 0x12, 0x18))
        };

        public SolidColorBrush BorderBrush => Type switch
        {
            ToastType.Success => new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0xE6, 0x5A)),
            ToastType.Warning => new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xB0, 0x20)),
            ToastType.Error => new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x4D, 0x4D)),
            _ => new SolidColorBrush(Color.FromArgb(0x80, 0x38, 0xBD, 0xF8))
        };
    }
}
