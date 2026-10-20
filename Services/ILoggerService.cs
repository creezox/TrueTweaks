using System.Collections.ObjectModel;
using TrueTweaks.Models;

namespace TrueTweaks.Services
{
    public interface ILoggerService
    {
        ObservableCollection<LogEntry> Logs { get; }
        void LogInfo(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        void Clear();
    }
}
