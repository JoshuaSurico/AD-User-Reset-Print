using AD_User_Reset_Print.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Tests.TestHelpers
{
    // Dummy ConsoleLogger for integration tests
    public class ConsoleLogger : ILoggingService
    {
        // Implement the Log method that was already there
        public void Log(string message, LogLevel level)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}][{level}] {message}");
        }

        // Corrected implementation: Change return type to IEnumerable<string>
        public IEnumerable<string> GetLogs()
        {
            // For a console logger, we don't store logs internally to retrieve them,
            // so return an empty list. This now matches the IEnumerable<string> interface.
            return [];
        }

        public event Action<string>? OnLogAdded; // Nullable event, so it might not need explicit handling if not used

        public void ResetErrorFlag()
        {
            // No internal error state to reset for a simple console logger.
        }

        public bool HasErrors
        {
            get { return false; } // A console logger won't typically track errors in this way.
        }
    }
}
