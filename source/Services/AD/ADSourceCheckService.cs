using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services.AD
{
    public class ADSourceCheckService
    {
        public event Action<string>? OnOutputMessage;

        private static readonly Guid ResetPasswordGuid = new("00299570-246d-11d0-a768-00aa006e0529");
        private static readonly Guid UnicodePwdGuid = new("bf967a0a-0de6-11d0-a285-00aa003049e2");
        private const int MemberSampleSize = 2;

        public async Task<PermissionCheckResult> RunPermissionCheckAsync(string domain, string username, string password, List<string> targetGroupNames)
        {
            var result = new PermissionCheckResult();
            OnOutputMessage?.Invoke($"Starting permission check for user '{username}' on domain '{domain}'...");

            try
            {
                await Task.Run(() =>
                {
                    // Step 1: Perform initial connection and find the user's AD object.
                    // This is a "fail-fast" check. If it fails, we stop immediately.
                    if (!TryInitialConnection(domain, username, password, result, out var domainRootEntry, out var userEntry))
                    {
                        return; // result object is already populated with the error message.
                    }

                    // If we get here, the initial bind and user lookup was successful.
                    result.IsSuccessful = true;

                    // Step 2: Get user's group memberships (SIDs) and check for high-privilege status.
                    if (!CheckUserPrivileges(userEntry!, domain, username, password, result, out var userSids))
                    {
                        return; // An error occurred getting group memberships. Stop.
                    }

                    // Step 3: Process each target group based on the user's privilege level.
                    ProcessTargetGroups(domainRootEntry!, username, password, targetGroupNames, userSids, result);
                });

                // Final check to ensure IsSuccessful is false if any error message was set during the process.
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result.IsSuccessful = false;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error: A critical unhandled error occurred during permission check: {ex.Message}";
                OnOutputMessage?.Invoke(errorMessage);
                System.Diagnostics.Debug.WriteLine($"Unhandled exception in RunPermissionCheck: {ex}");
                result.IsSuccessful = false;
                result.ErrorMessage = errorMessage;
            }
            finally
            {
                OnOutputMessage?.Invoke($"\nPermission check for '{username}' completed.");
            }
            return result;
        }

        /// <summary>
        /// Attempts to connect to the domain and find the specified user object.
        /// </summary>
        /// <returns>True if connection and user lookup are successful, otherwise false.</returns>
        private bool TryInitialConnection(string domain, string username, string password, PermissionCheckResult result, out DirectoryEntry? domainRootEntry, out DirectoryEntry? userEntry)
        {
            domainRootEntry = null;
            userEntry = null;

            OnOutputMessage?.Invoke($"\n--- Phase 1: Domain and User Validation ---");
            OnOutputMessage?.Invoke($"Attempting to ping domain controller at '{domain}'...");
            if (!IsDomainReachable(domain))
            {
                OnOutputMessage?.Invoke($"Warning: Domain controller for '{domain}' is not reachable via Ping. Proceeding with LDAP connection attempt...");
            }
            else
            {
                OnOutputMessage?.Invoke($"Successfully pinged domain controller at '{domain}'.");
            }

            try
            {
                OnOutputMessage?.Invoke($"Attempting to bind to domain '{domain}' with user '{username}'...");
                domainRootEntry = new DirectoryEntry($"LDAP://{domain}", username, password) { AuthenticationType = AuthenticationTypes.Secure };
                domainRootEntry.RefreshCache(); // Forces the connection
                OnOutputMessage?.Invoke($"Successfully bound to domain root: {domainRootEntry.Properties["distinguishedName"].Value ?? "N/A"}.");
            }
            catch (DirectoryServicesCOMException dse)
            {
                // Handle specific, common AD connection errors
                string msg = dse.ErrorCode switch
                {
                    -2147023570 => $"Error: Authentication failed for user '{username}'. Please check credentials. (Code: 0x{dse.ErrorCode:X})",
                    -2147016646 => $"Error: The AD server for '{domain}' is not operational or firewalled. (Code: 0x{dse.ErrorCode:X})",
                    _ => $"Error: AD Binding Error: {dse.Message} (Code: 0x{dse.ErrorCode:X})",
                };
                OnOutputMessage?.Invoke(msg);
                result.ErrorMessage = msg;
                return false;
            }

            userEntry = FindADObject(domainRootEntry, username, "user");
            if (userEntry == null)
            {
                string msg = $"Error: User '{username}' not found in domain '{domain}'.";
                OnOutputMessage?.Invoke(msg);
                result.ErrorMessage = msg;
                return false;
            }

            OnOutputMessage?.Invoke($"User object '{username}' found (DN: {userEntry.Properties["distinguishedName"].Value}).");
            return true;
        }

        /// <summary>
        /// Retrieves the user's security groups and checks for membership in highly-privileged groups.
        /// </summary>
        /// <returns>True if successful, otherwise false.</returns>
        private bool CheckUserPrivileges(DirectoryEntry userEntry, string domain, string username, string password, PermissionCheckResult result, out HashSet<SecurityIdentifier> userSids)
        {
            userSids = [];
            OnOutputMessage?.Invoke($"\n--- Phase 2: User Privilege Analysis ---");

            try
            {
                OnOutputMessage?.Invoke($"Enumerating group memberships for '{username}'...");
                userSids = GetAllUserGroupSids(userEntry, domain, username, password);
                OnOutputMessage?.Invoke($"Found {userSids.Count} total security group memberships (SIDs).");
            }
            catch (Exception ex)
            {
                string msg = $"Error enumerating group memberships: {ex.Message}";
                OnOutputMessage?.Invoke(msg);
                result.ErrorMessage = msg;
                return false;
            }

            var privilegedGroupMemberships = new List<string>();
            if (userSids.Any(sid => sid.IsWellKnown(WellKnownSidType.AccountDomainAdminsSid))) privilegedGroupMemberships.Add("Domain Admins");
            if (userSids.Any(sid => sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))) privilegedGroupMemberships.Add("Administrators");
            if (userSids.Any(sid => sid.IsWellKnown(WellKnownSidType.AccountEnterpriseAdminsSid))) privilegedGroupMemberships.Add("Enterprise Admins");

            result.IsHighlyPrivileged = privilegedGroupMemberships.Count > 0;
            if (result.IsHighlyPrivileged)
            {
                result.HasFullPermission = true;
                string groupsList = string.Join(", ", privilegedGroupMemberships);
                OnOutputMessage?.Invoke($"User IS a member of highly-privileged group(s): {groupsList}.");
                OnOutputMessage?.Invoke("--> Assuming full permissions for all target groups.");
            }
            else
            {
                OnOutputMessage?.Invoke("User is NOT a member of highly-privileged groups.");
                OnOutputMessage?.Invoke("--> Proceeding with specific ACL checks.");
            }
            return true;
        }

        /// <summary>
        /// Iterates through each target group and calls the appropriate permission check logic.
        /// </summary>
        private void ProcessTargetGroups(DirectoryEntry domainRootEntry, string username, string password, List<string> targetGroupNames, HashSet<SecurityIdentifier> userSids, PermissionCheckResult result)
        {
            OnOutputMessage?.Invoke($"\n--- Phase 3: Target Group Permission Checks ---");
            foreach (string groupName in targetGroupNames)
            {
                CheckPermissionsForSingleGroup(domainRootEntry, username, password, groupName, userSids, result);
            }
        }

        /// <summary>
        /// Performs the permission check for a single target group.
        /// </summary>
        private void CheckPermissionsForSingleGroup(DirectoryEntry domainRootEntry, string username, string password, string groupName, HashSet<SecurityIdentifier> userSids, PermissionCheckResult result)
        {
            OnOutputMessage?.Invoke($"\n--- Processing Target Group: {groupName} ---");
            using var targetGroupEntry = FindADObject(domainRootEntry, groupName, "group");

            if (targetGroupEntry == null)
            {
                OnOutputMessage?.Invoke($"  Warning: Target group '{groupName}' not found. Skipping.");
                result.TargetGroupPermissions[groupName] = false;
                result.ErrorMessage += $"\nTarget group '{groupName}' not found.";
                return;
            }

            OnOutputMessage?.Invoke($"  Found target group: {targetGroupEntry.Properties["distinguishedName"].Value}");

            if (result.IsHighlyPrivileged)
            {
                OnOutputMessage?.Invoke($"  -> Conclusion: User has assumed permissions (is Admin).");
                result.TargetGroupPermissions[groupName] = true;
                return;
            }

            List<string> memberDns = GetGroupMembers(targetGroupEntry);
            if (memberDns.Count == 0)
            {
                OnOutputMessage?.Invoke($"  Warning: Group '{groupName}' has no members or they could not be enumerated.");
                result.TargetGroupPermissions[groupName] = false;
                result.ErrorMessage += $"\nNo members found to check in group '{groupName}'.";
                return;
            }

            OnOutputMessage?.Invoke($"  Found {memberDns.Count} members. Sampling up to {MemberSampleSize} to check ACLs.");
            bool hasPermission = VerifyAclOnSampledMembers(domainRootEntry, memberDns, username, password, userSids);

            if (hasPermission)
            {
                OnOutputMessage?.Invoke($"  -> Conclusion for '{groupName}': YES, user has 'Reset Password' permission for sampled members.");
                result.TargetGroupPermissions[groupName] = true;
            }
            else
            {
                OnOutputMessage?.Invoke($"  -> Conclusion for '{groupName}': NO, user does not have permission for sampled members.");
                result.TargetGroupPermissions[groupName] = false;
                result.ErrorMessage += $"\nUser lacks permission for group '{groupName}'.";
            }
        }

        /// <summary>
        /// Takes a random sample of group members and verifies if the user has reset password rights on any of them.
        /// </summary>
        /// <returns>True if permission is found on at least one sampled member, otherwise false.</returns>
        private bool VerifyAclOnSampledMembers(DirectoryEntry domainRootEntry, List<string> memberDns, string username, string password, HashSet<SecurityIdentifier> userSids)
        {
            var rand = new Random();
            var membersToSample = memberDns.OrderBy(x => rand.Next()).Take(MemberSampleSize).ToList();

            foreach (string memberDn in membersToSample)
            {
                try
                {
                    // NEW STRATEGY: Use the existing authenticated connection to search for the member by its DN.
                    // This is the most reliable way to find an object without creating a new, problematic connection.
                    using var searcher = new DirectorySearcher(domainRootEntry)
                    {
                        // The distinguishedName must be escaped to be used in an LDAP filter.
                        Filter = $"(distinguishedName={EscapeLdapFilter(memberDn)})",
                        SearchScope = SearchScope.Subtree
                    };
                    // We don't need to load many properties, just enough to get the entry.
                    searcher.PropertiesToLoad.Add("sAMAccountName");

                    SearchResult? searchResult = searcher.FindOne();

                    if (searchResult != null)
                    {
                        // If the search was successful, get the DirectoryEntry from the result.
                        // This entry is guaranteed to be from a working connection.
                        using DirectoryEntry memberEntry = searchResult.GetDirectoryEntry();

                        string memberSam = memberEntry.Properties["sAMAccountName"]?.Value?.ToString() ?? "N/A";
                        OnOutputMessage?.Invoke($"    Checking ACL for user: {memberSam} (DN: {memberDn})");

                        if (CheckResetPasswordPermission(memberEntry, userSids))
                        {
                            OnOutputMessage?.Invoke($"      Permission found for {memberSam}.");
                            return true; // Success! We found permission.
                        }
                    }
                    else
                    {
                        OnOutputMessage?.Invoke($"    Warning: Could not find member object '{memberDn}' via search. It may have been deleted. Skipping.");
                    }
                }
                catch (Exception ex)
                {
                    // This will now catch any general errors with the search itself.
                    OnOutputMessage?.Invoke($"    Warning: An error occurred while searching for member '{memberDn}'. Skipping. Error: {ex.Message}");
                }
            }

            return false; // Looped through all samples and found no permissions.
        }


        // --- Helper Methods 

        private static string EscapeLdapFilter(string value)
        {
            // According to RFC 2254
            return value.Replace("\\", "\\5c")
                        .Replace("*", "\\2a")
                        .Replace("(", "\\28")
                        .Replace(")", "\\29")
                        .Replace("\0", "\\00");
        }

        private bool IsDomainReachable(string domain)
        {
            try
            {
                using var pingSender = new Ping();
                PingReply reply = pingSender.Send(domain, 2000);
                return reply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                OnOutputMessage?.Invoke($"Info: Ping to '{domain}' failed: {ex.Message}.");
                return false;
            }
        }

        private static DirectoryEntry? FindADObject(DirectoryEntry domainRootEntry, string sAMAccountName, string objectCategory)
        {
            using var searcher = new DirectorySearcher(domainRootEntry)
            {
                Filter = $"(&(objectCategory={objectCategory})(sAMAccountName={sAMAccountName}))",
                SearchScope = SearchScope.Subtree
            };
            searcher.PropertiesToLoad.Add("distinguishedName");
            searcher.PropertiesToLoad.Add("sAMAccountName");

            SearchResult? result = searcher.FindOne();
            return result?.GetDirectoryEntry();
        }

        private static List<string> GetGroupMembers(DirectoryEntry groupEntry)
        {
            var members = new List<string>();
            try
            {
                if (groupEntry.Invoke("Members") is not IEnumerable enumerableMembers) return members;
                foreach (object memberComObject in enumerableMembers)
                {
                    using var memberEntry = new DirectoryEntry(memberComObject);
                    if (memberEntry.Properties["distinguishedName"].Value is string dn)
                    {
                        members.Add(dn);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting members for group '{groupEntry.Name}': {ex.Message}");
            }
            return members;
        }

        private static HashSet<SecurityIdentifier> GetAllUserGroupSids(DirectoryEntry userEntry, string domainName, string username, string password)
        {
            var sids = new HashSet<SecurityIdentifier>();
            try
            {
                using var context = new PrincipalContext(ContextType.Domain, domainName, username, password);
                string? samAccountName = userEntry.Properties["sAMAccountName"]?.Value?.ToString();
                if (string.IsNullOrEmpty(samAccountName)) return sids;

                using var userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);
                if (userPrincipal == null) return sids;

                foreach (Principal group in userPrincipal.GetAuthorizationGroups())
                {
                    if (group.Sid != null) sids.Add(group.Sid);
                    group.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating group SIDs for '{username}': {ex.Message}");
                throw; // Re-throw to be caught by the calling method.
            }
            return sids;
        }

        private static bool CheckResetPasswordPermission(DirectoryEntry targetUserEntry, HashSet<SecurityIdentifier> userSids)
        {
            try
            {
                targetUserEntry.RefreshCache(["nTSecurityDescriptor"]);
                var security = targetUserEntry.ObjectSecurity;

                foreach (ActiveDirectoryAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType == AccessControlType.Allow &&
                        rule.IdentityReference is SecurityIdentifier ruleSid &&
                        userSids.Contains(ruleSid))
                    {
                        bool hasResetRight = (rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.ExtendedRight) && (rule.ObjectType.Equals(ResetPasswordGuid) || rule.ObjectType.Equals(Guid.Empty)));
                        bool hasWritePwdRight = (rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.WriteProperty) && rule.ObjectType.Equals(UnicodePwdGuid));

                        if (hasResetRight || hasWritePwdRight)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking permissions for {targetUserEntry.Name}: {ex.Message}");
            }
            return false;
        }
    }

    public class PermissionCheckResult
    {
        public bool IsSuccessful { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool HasFullPermission { get; set; } = false;
        public bool IsHighlyPrivileged { get; set; } = false;
        public Dictionary<string, bool> TargetGroupPermissions { get; set; } = [];
    }
}