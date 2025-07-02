// File: AD_User_Reset_Print.Tests/TestHelpers/TestLogger.cs

using AD_User_Reset_Print.Services; // Adjust namespace if your ILoggingService is elsewhere
using System;
using System.Collections.Generic;
using System.Linq;

namespace AD_User_Reset_Print.Tests.TestHelpers
{
    public class TestLogger : ILoggingService
    {
        public List<(string Message, LogLevel Level)> Logs { get; } = [];

        public void Log(string message, LogLevel level)
        {
            Logs.Add((message, level));
            // You could also Console.WriteLine here for debugging during test runs
            // Console.WriteLine($"[{level}] {message}");

            // If any subscribers, trigger the event (though not typically used in TestLogger for direct testing)
            OnLogAdded?.Invoke(message);
        }

        public void ClearLogs()
        {
            Logs.Clear();
        }

        public bool ContainsLog(string messagePart, LogLevel? level = null)
        {
            return Logs.Any(l => l.Message.Contains(messagePart) && (level == null || l.Level == level.Value));
        }

        // --- Missing ILoggingService Members Implementation ---

        // Implements 'ILoggingService.GetLogs()'
        public IEnumerable<string> GetLogs()
        {
            // Return only the messages from the captured logs
            return Logs.Select(l => l.Message);
        }

        // Implements 'ILoggingService.OnLogAdded' event
        public event Action<string>? OnLogAdded;

        // Implements 'ILoggingService.ResetErrorFlag()'
        public void ResetErrorFlag()
        {
            // For a TestLogger, this might not have a meaningful operation
            // unless you add internal error tracking. For now, it does nothing.
        }

        // Implements 'ILoggingService.HasErrors' property
        public bool HasErrors
        {
            // Corrected: Removed reference to LogLevel.Critical as it's not in your enum definition.
            get { return Logs.Any(l => l.Level == LogLevel.Error); }
        }
    }
}