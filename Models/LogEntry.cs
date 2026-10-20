using System;
using System.Windows.Media;

namespace TrueTweaks.Models
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; } = LogLevel.Info;

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        public string Prefix => Level switch
        {
            LogLevel.Success => "[SUCCESS]",
            LogLevel.Warning => "[WARN]",
            LogLevel.Error => "[ERROR]",
            _ => "[INFO]"
        };

        public SolidColorBrush Color => Level switch
        {
            LogLevel.Success => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xE6, 0x5A)), // Vert #00E65A
            LogLevel.Warning => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB0, 0x20)), // Orange #FFB020
            LogLevel.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x4D, 0x4D)),   // Rouge #FF4D4D
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0xBD, 0xF8))                // Bleu Cyan doux #38BDF8
        };

        public SolidColorBrush TimestampColor => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x52, 0x60, 0x58)); // Gris discret #526058
        public SolidColorBrush MessageColor => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD1, 0xDB, 0xD5));   // Blanc cassé #D1DBD5
    }
}
