// File: Services/ILoggingService.cs
namespace AD_User_Reset_Print.Services
{
    // Define the LogLevel enum here, as it's directly related to logging operations.
    public enum LogLevel { Info, Warning, Error, Debug }

    // Define the ILoggingService interface.
    public interface ILoggingService
    {
        void Log(string message, LogLevel level = LogLevel.Info);
        IEnumerable<string> GetLogs();
        event Action<string> OnLogAdded;
        void ResetErrorFlag();
        bool HasErrors { get; }
    }
}