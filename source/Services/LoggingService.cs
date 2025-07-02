// File: Services/LoggingService.cs
using AD_User_Reset_Print.Models; // Assuming AppSettings is in Models
using System.IO;
// Note: The InternalsVisibleTo attribute is typically placed in a single file like AssemblyInfo.cs
// or directly in the .csproj file. If your project setup puts it in a service file,
// ensure it's not duplicated across multiple service files unnecessarily.
// For this example, I'll include it as you had it previously.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AD-User-Reset-Print.Tests")]

namespace AD_User_Reset_Print.Services
{
    // Implement the ILoggingService interface
    public class LoggingService : ILoggingService
    {
        private readonly List<string> _logMessages = [];
        private readonly object _lock = new();
        private readonly string _logFilePath;

        public event Action<string> OnLogAdded;
        public bool HasErrors { get; private set; }

        // Primary constructor: Uses AppSettings for the log directory
        public LoggingService() : this(AppSettings.LogDirectory)
        {
        }

        // Secondary constructor: Allows specifying the log directory (useful for testing)
        // This is marked 'internal' so only your test project (and this assembly) can access it.
        internal LoggingService(string logDirectory)
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
                _logFilePath = Path.Combine(logDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");

                if (File.Exists(_logFilePath))
                {
                    _logMessages.AddRange(File.ReadAllLines(_logFilePath));
                    if (_logMessages.Any(log => log.Contains("[ERROR]"))) HasErrors = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL FAILURE in LoggingService constructor: {ex.Message}");
            }
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] [{level.ToString().ToUpper()}]: {message}";
            if (level == LogLevel.Error) HasErrors = true;

            lock (_lock)
            {
                _logMessages.Add(formattedMessage);
                try
                {
                    File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write to log file {_logFilePath}: {ex.Message}");
                }
            }
            OnLogAdded?.Invoke(formattedMessage);
        }

        public IEnumerable<string> GetLogs()
        {
            lock (_lock)
            {
                return [.. _logMessages];
            }
        }

        /// <summary>
        /// Resets the HasErrors flag to false. Should be called before
        /// starting a new user-initiated operation.
        /// </summary>
        public void ResetErrorFlag()
        {
            lock (_lock)
            {
                HasErrors = false;
            }
        }
    }
}