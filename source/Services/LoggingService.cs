using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services
{
    public enum LogLevel { Info, Warning, Error }

    public static class LoggingService
    {
        private static readonly List<string> _logMessages = [];
        private static readonly object _lock = new();

        // Event that the LogsWindow will subscribe to
        public static event Action<string> OnLogAdded;

        public static bool HasErrors { get; private set; }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] [{level.ToString().ToUpper()}]: {message}";

            if (level == LogLevel.Error)
            {
                HasErrors = true;
            }

            lock (_lock)
            {
                _logMessages.Add(formattedMessage);
            }

            // Raise the event to notify any open log windows
            OnLogAdded?.Invoke(formattedMessage);
        }

        public static IEnumerable<string> GetLogs()
        {
            lock (_lock)
            {
                return [.. _logMessages]; // Return a copy
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _logMessages.Clear();
                HasErrors = false;
            }
        }
    }
}