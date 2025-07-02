using System.IO;

namespace AD_User_Reset_Print.Models
{
    public static class AppSettings
    {
        /// <summary>
        /// The root directory for all application data.
        /// </summary>
        public static readonly string AppDataDirectory = @"C:\AD-User-Reset-Print";

        /// <summary>
        /// The directory where historical logs are stored.
        /// </summary>
        public static readonly string LogDirectory = Path.Combine(AppDataDirectory, "Logs");

        /// <summary>
        /// The directory where users are stored.
        /// </summary>
        public static readonly string UserListDirectory = Path.Combine(AppDataDirectory, "UsersLists");
        public static readonly string UserListFilePath = Path.Combine(AppDataDirectory, "UsersLists", "UserList.json");
    }
}
