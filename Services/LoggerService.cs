using System;
using System.Collections.ObjectModel;
using System.Windows;
using TrueTweaks.Models;

namespace TrueTweaks.Services
{
    public class LoggerService : ILoggerService
    {
        private const int MaxLogEntries = 500;
        private readonly object _lock = new();

        public ObservableCollection<LogEntry> Logs { get; } = new();

        public void LogInfo(string message) => Append(message, LogLevel.Info);
        public void LogSuccess(string message) => Append(message, LogLevel.Success);
        public void LogWarning(string message) => Append(message, LogLevel.Warning);
        public void LogError(string message) => Append(message, LogLevel.Error);

        public void Clear()
        {
            void ClearAction() => Logs.Clear();

            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                Application.Current.Dispatcher.Invoke(ClearAction);
            }
            else
            {
                ClearAction();
            }
        }

        private void Append(string message, LogLevel level)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            };

            void AddAction()
            {
                lock (_lock)
                {
                    if (Logs.Count >= MaxLogEntries)
                    {
                        Logs.RemoveAt(0);
                    }
                    Logs.Add(entry);
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(AddAction));
            }
            else
            {
                AddAction();
            }
        }
    }
}
