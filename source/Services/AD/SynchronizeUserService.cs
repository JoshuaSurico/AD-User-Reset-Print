// SynchronizeUserService.cs
using AD_User_Reset_Print.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services.AD
{
    internal class SynchronizeUserService
    {
        private readonly string _userListPath;

        public SynchronizeUserService()
        {
            // Ensure the directory exists
            Directory.CreateDirectory(AppSettings.UserListDirectory);
            _userListPath = AppSettings.UserListFilePath;
        }

        public async Task<List<User>> Sync(IProgress<ProgressReport> progress)
        {
            var allUsers = new List<User>();
            List<CredentialEntry> credentials = CredentialStorageService.LoadCredentials();

            var groupProcessingTasks = new List<Task<List<User>>>();

            progress.Report(new ProgressReport { PercentComplete = 0, CurrentActivity = "Queueing group processing..." });

            // Build the list of tasks first
            foreach (var credential in credentials)
            {
                foreach (var groupName in credential.Groups)
                {
                    groupProcessingTasks.Add(GetADUsersAsync(groupName, credential.Domain, credential.Username, credential.Password, progress));
                }
            }

            int totalTasks = groupProcessingTasks.Count;
            int tasksCompleted = 0;

            // Process tasks one by one as they complete, allowing for real-time progress updates
            while (groupProcessingTasks.Count != 0)
            {
                // Wait for ANY task to finish
                Task<List<User>> finishedTask = await Task.WhenAny(groupProcessingTasks);

                // Remove it from the list of running tasks
                groupProcessingTasks.Remove(finishedTask);
                tasksCompleted++;

                // Add its results to our main list
                var userList = await finishedTask; // Get the result from the already-completed task
                if (userList != null)
                {
                    allUsers.AddRange(userList);
                }

                // Report overall progress
                progress.Report(new ProgressReport
                {
                    PercentComplete = (int)((double)tasksCompleted / totalTasks * 95), // Go up to 95%
                    CurrentActivity = $"Processed {tasksCompleted} of {totalTasks} groups..."
                });
            }

            progress.Report(new ProgressReport { PercentComplete = 95, CurrentActivity = "Finalizing and saving user list..." });
            JsonManagerService.SaveToJson(allUsers, _userListPath);

            // The final report will be handled by the UI, so we just return the data.
            return allUsers;
        }

        // This method now returns a list of users, making it self-contained and testable.
        private async Task<List<User>> GetADUsersAsync(string groupName, string domain, string username, string password, IProgress<ProgressReport> progress)
        {
            var usersInGroup = new List<User>();
            LoggingService.Log($"Starting optimized search for Group: '{groupName}' in Domain: '{domain}'");

            try
            {
                // Use a DirectoryEntry with the specific credentials for searching
                using var rootEntry = new DirectoryEntry($"LDAP://{domain}", username, password, AuthenticationTypes.Secure);

                // 1. Find the Distinguished Name (DN) of the group first.
                string groupDn;
                using (var groupSearcher = new DirectorySearcher(rootEntry, $"(&(objectCategory=group)(sAMAccountName={groupName}))"))
                {
                    groupSearcher.PropertiesToLoad.Add("distinguishedName");
                    var groupResult = groupSearcher.FindOne();
                    if (groupResult == null)
                    {
                        LoggingService.Log($"Group '{groupName}' not found.", LogLevel.Error);
                        return usersInGroup;
                    }
                    groupDn = groupResult.Properties["distinguishedName"][0].ToString();
                }
                LoggingService.Log($"  Found group '{groupName}' at DN: {groupDn}");

                // 2. Now search for all enabled users who are members of that group.
                using (var userSearcher = new DirectorySearcher(rootEntry))
                {
                    // This filter is highly efficient. It finds users who are direct or nested members of the group.
                    userSearcher.Filter = $"(&(objectCategory=person)(objectClass=user)(memberOf:1.2.840.113556.1.4.1941:={groupDn})(!userAccountControl:1.2.840.113556.1.4.803:=2))";

                    // CRUCIAL FOR PERFORMANCE: Only load the properties you absolutely need.
                    userSearcher.PropertiesToLoad.Add("sAMAccountName");
                    userSearcher.PropertiesToLoad.Add("displayName");
                    userSearcher.PropertiesToLoad.Add("givenName");
                    userSearcher.PropertiesToLoad.Add("sn");
                    userSearcher.PropertiesToLoad.Add("mail");
                    userSearcher.PropertiesToLoad.Add("extensionAttribute2");
                    userSearcher.PropertiesToLoad.Add("description");

                    // Paging is essential for large groups to avoid timeout errors.
                    userSearcher.PageSize = 1000;

                    LoggingService.Log($"  Searching for users in group...");
                    using (var searchResults = userSearcher.FindAll())
                    {
                        LoggingService.Log($"  Found {searchResults.Count} total users. Processing...");
                        int processedCount = 0;
                        foreach (SearchResult result in searchResults)
                        {
                            processedCount++;
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
                LoggingService.Log($"  Finished processing {usersInGroup.Count} enabled users from '{groupName}'.");
            }
            catch (Exception ex)
            {
                LoggingService.Log($"CRITICAL FAILURE in group '{groupName}': {ex.Message}", LogLevel.Error);
                LoggingService.Log($"Stack Trace: {ex}", LogLevel.Error);
            }

            return usersInGroup;
        }
    }
}