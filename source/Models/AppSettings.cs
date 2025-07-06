using System;
using System.IO;

namespace AD_User_Reset_Print.Models
{
    /// <summary>
    /// Provides centralized, read-only access to application settings, primarily file and directory paths.
    /// This class ensures all parts of the application reference the same locations for data storage.
    /// </summary>
    public static class AppSettings
    {
        // Define the application's unique folder name to be used within AppData.
        private const string AppName = "AD-User-Reset-Print";

        // 1. DEFINE THE BASE DATA PATH
        // We use Environment.SpecialFolder.LocalApplicationData to get the path to the current user's
        // local AppData folder (e.g., C:\Users\YourUsername\AppData\Local).
        // This ensures that each user on the machine has their own separate data, which is ideal
        // for storing per-user credentials, caches, and logs.
        private static readonly string BaseDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName
        );

        // --- PUBLIC DIRECTORY PATHS ---

        /// <summary>
        /// The absolute root directory for all application data for the current user.
        /// e.g., C:\Users\CurrentUser\AppData\Local\AD-User-Reset-Print
        /// </summary>
        public static readonly string AppDataDirectory = BaseDataPath;

        /// <summary>
        /// The directory where historical log files are stored.
        /// e.g., C:\Users\CurrentUser\AppData\Local\AD-User-Reset-Print\Logs
        /// </summary>
        public static readonly string LogDirectory = Path.Combine(BaseDataPath, "Logs");

        /// <summary>
        /// The directory where the cached user list JSON file is stored.
        /// e.g., C:\Users\CurrentUser\AppData\Local\AD-User-Reset-Print\UsersLists
        /// </summary>
        public static readonly string UserListDirectory = Path.Combine(BaseDataPath, "UsersLists");

        // --- PUBLIC FILE PATHS ---

        /// <summary>
        /// The full path to the user list JSON file, which acts as a local cache.
        /// e.g., C:\Users\CurrentUser\AppData\Local\AD-User-Reset-Print\UsersLists\UserList.json
        /// </summary>
        public static readonly string UserListFilePath = Path.Combine(UserListDirectory, "UserList.json");


        // 2. STATIC CONSTRUCTOR FOR AUTO-INITIALIZATION
        // This constructor runs only once, the very first time any member of the AppSettings class is accessed.
        // It's the perfect place to perform one-time setup, like ensuring all necessary directories exist.
        // Directory.CreateDirectory is safe to call even if the folders already exist; it will do nothing in that case.
        static AppSettings()
        {
            Directory.CreateDirectory(AppDataDirectory);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(UserListDirectory);
        }
    }
}