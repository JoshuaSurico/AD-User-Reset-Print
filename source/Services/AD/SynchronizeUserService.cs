// File: Services.AD/SynchronizeUserService.cs
using AD_User_Reset_Print.Models;
using System.DirectoryServices; // For DirectoryEntry, DirectorySearcher
using System.IO; // For Directory.CreateDirectory
using System.Runtime.InteropServices; // For Marshal (SecureString to string conversion)
using System.Security; // For SecureString

namespace AD_User_Reset_Print.Services.AD
{
    // Make the class public and implement the interface
    public class SynchronizeUserService : ISynchronizeUserService
    {
        private readonly string _userListPath;
        private readonly ILoggingService _logger;
        private readonly ICredentialStorageService _credentialStorageService;
        private readonly IJsonManagerService _jsonManagerService;

        public SynchronizeUserService(ILoggingService logger, ICredentialStorageService credentialStorageService, IJsonManagerService jsonManagerService)
        {
            _logger = logger;
            _credentialStorageService = credentialStorageService;
            _jsonManagerService = jsonManagerService;

            // Ensure the directory exists
            // This might be better placed in AppSettings or a dedicated config service
            // if it's application-wide setup, but fine here for now.
            Directory.CreateDirectory(AppSettings.UserListDirectory);
            _userListPath = AppSettings.UserListFilePath;
        }

        public async Task<List<User>> Sync(IProgress<ProgressReport> progress)
        {
            var allUsers = new List<User>();
            List<CredentialEntry> credentials = _credentialStorageService.LoadCredentials();

            // Use a list to hold the tasks for processing groups concurrently
            var groupProcessingTasks = new List<Task<List<User>>>();

            progress.Report(new ProgressReport { PercentComplete = 0, CurrentActivity = "Queueing group processing..." });

            // Build the list of tasks for each group using the loaded credentials
            foreach (var credential in credentials)
            {
                // Basic check for null password, though CredentialEntry's default ensures new SecureString
                if (credential.Password == null)
                {
                    _logger.Log($"Credential for domain '{credential.Domain}' username '{credential.Username}' has a null password. Skipping groups for this credential.", LogLevel.Warning);
                    continue; // Skip this credential if password is null
                }

                foreach (var groupName in credential.Groups)
                {
                    // Pass the SecureString directly
                    groupProcessingTasks.Add(GetADUsersAsync(groupName, credential.Domain, credential.Username, credential.Password, progress));
                }
            }

            int totalTasks = groupProcessingTasks.Count;
            int tasksCompleted = 0;

            if (totalTasks == 0)
            {
                _logger.Log("No groups configured for synchronization. Returning empty list.", LogLevel.Info);
                progress.Report(new ProgressReport { PercentComplete = 100, CurrentActivity = "No groups to sync." });
                return allUsers;
            }

            // Process tasks one by one as they complete, allowing for real-time progress updates
            while (groupProcessingTasks.Count != 0)
            {
                // Wait for ANY task to finish
                Task<List<User>> finishedTask = await Task.WhenAny(groupProcessingTasks);

                // Remove it from the list of running tasks
                groupProcessingTasks.Remove(finishedTask);
                tasksCompleted++;

                // Handle potential exceptions from the finished task
                if (finishedTask.IsFaulted)
                {
                    // Log the exception from the background task
                    _logger.Log($"An error occurred while processing a group: {finishedTask.Exception?.InnerException?.Message ?? finishedTask.Exception?.Message}", LogLevel.Error);
                    System.Diagnostics.Debug.WriteLine($"Group processing task faulted: {finishedTask.Exception}");
                }
                else if (finishedTask.IsCanceled)
                {
                    _logger.Log("A group processing task was cancelled.", LogLevel.Warning);
                }
                else // Task completed successfully
                {
                    List<User>? userList = await finishedTask;
                    if (userList != null)
                    {
                        allUsers.AddRange(userList.Except(allUsers, new UserEqualityComparer())); // Avoid duplicates
                    }
                }

                // Report overall progress. Keep it slightly below 100% until all tasks are done.
                progress.Report(new ProgressReport
                {
                    PercentComplete = (int)((double)tasksCompleted / totalTasks * 95), // Go up to 95%
                    CurrentActivity = $"Processed {tasksCompleted} of {totalTasks} groups..."
                });
            }

            // Dispose of credentials after all tasks are complete
            // This loop ensures SecureString objects are cleared.
            foreach (var credential in credentials)
            {
                credential?.Dispose();
            }
            credentials.Clear(); // Clear the list as objects are disposed

            progress.Report(new ProgressReport { PercentComplete = 95, CurrentActivity = "Finalizing and saving user list..." });
            // Remove duplicates again after adding all users from various groups
            allUsers = [.. allUsers.Distinct(new UserEqualityComparer())];
            _jsonManagerService.SaveToJson(allUsers, _userListPath, overwrite: true);

            progress.Report(new ProgressReport { PercentComplete = 100, CurrentActivity = $"Synchronization complete. Found {allUsers.Count} unique users." });
            return allUsers;
        }

        /// <summary>
        /// Retrieves enabled users who are members of a specific AD group.
        /// </summary>
        private async Task<List<User>> GetADUsersAsync(string groupName, string domain, string username, SecureString password, IProgress<ProgressReport> progress)
        {
            var usersInGroup = new List<User>();
            _logger.Log($"Starting optimized search for Group: '{groupName}' in Domain: '{domain}'");

            // CRUCIAL CHECK: Ensure the provided SecureString actually contains a password.
            if (password == null || password.Length == 0)
            {
                _logger.Log($"Password for user '{username}' in domain '{domain}' is empty. Cannot authenticate. Skipping group '{groupName}'.", LogLevel.Error);
                // Return an empty list immediately to indicate failure for this group.
                return usersInGroup;
            }

            IntPtr passwordPtr = IntPtr.Zero;
            string? adminPasswordPlaintext;

            try
            {
                // Convert SecureString to plaintext string for DirectoryEntry
                passwordPtr = Marshal.SecureStringToBSTR(password);
                adminPasswordPlaintext = Marshal.PtrToStringBSTR(passwordPtr);

                // This check is a safeguard, but the initial check is more important.
                if (string.IsNullOrEmpty(adminPasswordPlaintext))
                {
                    // This error means the SecureString was empty, which we now check for earlier.
                    _logger.Log($"CRITICAL DEBUG: The plaintext password converted from SecureString for user '{username}' is NULL or EMPTY.", LogLevel.Error);
                    throw new InvalidOperationException("Password could not be decrypted for AD operations.");
                }

                using (var rootEntry = new DirectoryEntry($"LDAP://{domain}", username, adminPasswordPlaintext, AuthenticationTypes.Secure))
                {
                    // 1. Find the Distinguished Name (DN) of the group first.
                    string groupDn;
                    using (var groupSearcher = new DirectorySearcher(rootEntry, $"(&(objectCategory=group)(sAMAccountName={groupName}))"))
                    {
                        groupSearcher.PropertiesToLoad.Add("distinguishedName");
                        var groupResult = groupSearcher.FindOne();
                        if (groupResult == null)
                        {
                            _logger.Log($"Group '{groupName}' not found in domain '{domain}'.", LogLevel.Error);
                            return usersInGroup;
                        }
                        groupDn = groupResult.Properties["distinguishedName"][0].ToString();
                    }
                    _logger.Log($"  Found group '{groupName}' at DN: {groupDn}");

                    // 2. Now search for all enabled users who are members of that group.
                    using (var userSearcher = new DirectorySearcher(rootEntry))
                    {
                        userSearcher.Filter = $"(&(objectCategory=person)(objectClass=user)(memberOf:1.2.840.113556.1.4.1941:={groupDn})(!userAccountControl:1.2.840.113556.1.4.803:=2))";
                        userSearcher.PropertiesToLoad.Add("sAMAccountName");
                        userSearcher.PropertiesToLoad.Add("displayName");
                        userSearcher.PropertiesToLoad.Add("givenName");
                        userSearcher.PropertiesToLoad.Add("sn");
                        userSearcher.PropertiesToLoad.Add("mail");
                        userSearcher.PropertiesToLoad.Add("extensionAttribute2");
                        userSearcher.PropertiesToLoad.Add("description");
                        userSearcher.PageSize = 1000;

                        _logger.Log($"  Searching for users in group '{groupName}'...");
                        using (var searchResults = userSearcher.FindAll())
                        {
                            _logger.Log($"  Found {searchResults.Count} total users in '{groupName}'. Processing...");
                            foreach (SearchResult result in searchResults)
                            {
                                usersInGroup.Add(new User(
                                    domain: domain,
                                    sAMAccountName: result.Properties.Contains("sAMAccountName") ? result.Properties["sAMAccountName"][0].ToString() : string.Empty,
                                    displayName: result.Properties.Contains("displayName") ? result.Properties["displayName"][0].ToString() : string.Empty,
                                    givenName: result.Properties.Contains("givenName") ? result.Properties["givenName"][0].ToString() : string.Empty,
                                    sn: result.Properties.Contains("sn") ? result.Properties["sn"][0].ToString() : string.Empty,
                                    mail: result.Properties.Contains("mail") ? result.Properties["mail"][0].ToString() : string.Empty,
                                    title: result.Properties.Contains("extensionAttribute2") ? result.Properties["extensionAttribute2"][0].ToString() : string.Empty,
                                    description: result.Properties.Contains("description") ? result.Properties["description"][0].ToString() : string.Empty,
                                    userGroups: []
                                ));
                            }
                        }
                    }
                    _logger.Log($"  Finished processing {usersInGroup.Count} enabled users from '{groupName}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"CRITICAL FAILURE while processing group '{groupName}' in domain '{domain}': {ex.Message}", LogLevel.Error);
                _logger.Log($"Stack Trace: {ex}", LogLevel.Error);
            }
            finally
            {
                if (passwordPtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(passwordPtr);
                }
                adminPasswordPlaintext = null;
            }

            return usersInGroup;
        }

        // Helper for User equality to avoid duplicates when merging user lists from different groups
        private class UserEqualityComparer : IEqualityComparer<User>
        {
            public bool Equals(User x, User y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                // Consider users equal if their sAMAccountName and Domain match
                return x.SAMAccountName == y.SAMAccountName && x.Domain == y.Domain;
            }

            public int GetHashCode(User obj)
            {
                if (obj == null) return 0;
                // Combine hash codes of SAMAccountName and Domain
                return HashCode.Combine(obj.SAMAccountName, obj.Domain);
            }
        }
    }
}