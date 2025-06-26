using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Linq;
using System.Security.AccessControl; // This is for ActiveDirectoryAccessRule, ActiveDirectorySecurity
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services
{
    // Make the class public so it can be accessed from outside the assembly (e.g., from Login.xaml.cs)
    public class LoginCheckService
    {
        public event Action<string>? OnOutputMessage;
        public event Action<string>? OnErrorOccurred;

        // Make the GUIDs constants within this service, as they are specific to AD operations
        private static readonly Guid ResetPasswordGuid = new("00299570-246d-11d0-a768-00aa006e0529");
        private static readonly Guid UnicodePwdGuid = new("bf967a0a-0de6-11d0-a285-00aa003049e2");

        public async Task<PermissionCheckResult> RunPermissionCheckAsync(string domain, string username, string password, List<string> targetGroupNames)
        {
            var result = new PermissionCheckResult();

            OnOutputMessage?.Invoke($"Starting permission check for user '{username}' on domain '{domain}'...");

            try
            {
                await Task.Run(() => // No need for 'async' here unless you're awaiting inside this Task.Run lambda.
                {
                    // Authenticate and get the DirectoryEntry for the domain root for initial binding
                    using (DirectoryEntry domainRootEntry = new DirectoryEntry($"LDAP://{domain}", username, password)) // *** FIX: Use method parameters ***
                    {
                        domainRootEntry.AuthenticationType = AuthenticationTypes.Secure; // Ensure secure authentication
                        domainRootEntry.RefreshCache(); // Ensure properties are fresh

                        string domainDN = domainRootEntry.Properties["distinguishedName"].Value.ToString();
                        OnOutputMessage?.Invoke($"Successfully bound to domain root: {domainDN} with provided credentials."); // *** FIX: Use OnOutputMessage event ***

                        // Find the user for whom permissions are being checked (e.g., 'sur')
                        using (DirectoryEntry userEntry = FindADObject(domainRootEntry, username, "user")) // *** FIX: Use method parameter 'username' ***
                        {
                            if (userEntry == null)
                            {
                                string msg = $"Error: User '{username}' not found in domain '{domain}'. Cannot proceed with permission checks.";
                                OnOutputMessage?.Invoke(msg); // *** FIX: Use OnOutputMessage event ***
                                OnErrorOccurred?.Invoke(msg);
                                result.IsSuccessful = false; // Set result for early exit
                                result.ErrorMessage = msg;
                                return; // Exit the Task.Run block
                            }
                            OnOutputMessage?.Invoke($"User '{username}' found (DN: {userEntry.Properties["distinguishedName"].Value})."); // *** FIX: Use OnOutputMessage event ***

                            // Get all SIDs for the user and their groups (recursive)
                            HashSet<SecurityIdentifier> userSids = GetAllUserGroupSids(userEntry, domain, username, password); // *** FIX: Use method parameters ***

                            // Also explicitly add the user's own SID to userSids, as they might have direct permissions
                            try
                            {
                                userSids.Add(new NTAccount(username).Translate(typeof(SecurityIdentifier)) as SecurityIdentifier); // *** FIX: Use method parameter 'username' ***
                            }
                            catch (IdentityNotMappedException)
                            {
                                System.Diagnostics.Debug.WriteLine($"Warning: Could not map user '{username}' to a SecurityIdentifier directly.");
                            }

                            OnOutputMessage?.Invoke($"Found {userSids.Count} SIDs for '{username}' (including self and groups)."); // *** FIX: Use OnOutputMessage event ***
                            foreach (var sid in userSids)
                            {
                                try
                                {
                                    string name = sid.Translate(typeof(NTAccount)).ToString();
                                    OnOutputMessage?.Invoke($"  - SID: {sid.Value} ({name})"); // *** FIX: Use OnOutputMessage event ***
                                }
                                catch (System.Security.Principal.IdentityNotMappedException)
                                {
                                    OnOutputMessage?.Invoke($"  - SID: {sid.Value} (Cannot map to NTAccount - e.g., WellKnown SID or foreign domain)"); // *** FIX: Use OnOutputMessage event ***
                                }
                            }

                            // Initialize a list to store the names of privileged groups the user belongs to
                            List<string> privilegedGroupMemberships = new List<string>();

                            // Check for membership in each highly privileged group and add to the list if found
                            // Note: Relying purely on SID endings (-512, -519) for Domain Admins/Enterprise Admins is generally reliable,
                            // but for Builtin Administrators (S-1-5-32-544), checking the full SID is correct.
                            if (userSids.Any(sid => sid.Value.EndsWith("-512")))
                            {
                                privilegedGroupMemberships.Add("Domain Admins");
                            }
                            if (userSids.Any(sid => sid.Value == "S-1-5-32-544"))
                            {
                                privilegedGroupMemberships.Add("Builtin\\Administrators");
                            }
                            if (userSids.Any(sid => sid.Value.EndsWith("-519")))
                            {
                                privilegedGroupMemberships.Add("Enterprise Admins");
                            }

                            // Determine if the user is highly privileged based on any found memberships
                            bool isUserHighlyPrivileged = privilegedGroupMemberships.Any(); // True if the list is not empty
                            result.IsHighlyPrivileged = isUserHighlyPrivileged; // Store this in the result object

                            if (isUserHighlyPrivileged)
                            {
                                string groupsList = string.Join(", ", privilegedGroupMemberships);
                                OnOutputMessage?.Invoke($"\nUser '{username}' IS a member of a highly privileged administrative group(s): {groupsList}."); // *** FIX: Use OnOutputMessage event ***
                                OnOutputMessage?.Invoke("  Assuming full password reset permissions for all target groups."); // *** FIX: Use OnOutputMessage event ***
                                result.HasFullPermission = true; // User is highly privileged, assume full control
                            }
                            else
                            {
                                OnOutputMessage?.Invoke($"\nUser '{username}' is NOT a member of recognized highly privileged administrative groups."); // *** FIX: Use OnOutputMessage event ***
                                OnOutputMessage?.Invoke("  Proceeding with specific ACL checks for 'Reset Password' extended right."); // *** FIX: Use OnOutputMessage event ***
                            }

                            foreach (string groupName in targetGroupNames) // *** FIX: Use method parameter 'targetGroupNames' ***
                            {
                                OnOutputMessage?.Invoke($"\n--- Processing Target Group: {groupName} ---"); // *** FIX: Use OnOutputMessage event ***
                                using (DirectoryEntry targetGroupEntry = FindADObject(domainRootEntry, groupName, "group"))
                                {
                                    if (targetGroupEntry == null)
                                    {
                                        OnOutputMessage?.Invoke($"  Warning: Target group '{groupName}' not found in domain '{domain}'. Skipping this group."); // *** FIX: Use OnOutputMessage event ***
                                        continue;
                                    }
                                    OnOutputMessage?.Invoke($"  Found target group: {targetGroupEntry.Properties["distinguishedName"].Value}"); // *** FIX: Use OnOutputMessage event ***

                                    if (isUserHighlyPrivileged)
                                    {
                                        OnOutputMessage?.Invoke($"  -> Conclusion for group '{groupName}': User '{username}' (highly privileged admin) has assumed permission to reset passwords for its members."); // *** FIX: Use OnOutputMessage event ***
                                        result.TargetGroupPermissions[groupName] = true; // Mark as true if highly privileged
                                        continue; // Go to the next target group
                                    }

                                    // If not highly privileged, proceed with detailed ACL check on sampled members
                                    List<string> memberDns = GetGroupMembers(targetGroupEntry);
                                    if (memberDns.Any())
                                    {
                                        OnOutputMessage?.Invoke($"  Found {memberDns.Count} members in group '{groupName}'."); // *** FIX: Use OnOutputMessage event ***
                                        const int sampleSize = 2; // How many random users to test via ACL check
                                        Random rand = new Random();
                                        List<string> membersToSample = memberDns.OrderBy(x => rand.Next()).Take(sampleSize).ToList();

                                        if (!membersToSample.Any())
                                        {
                                            OnOutputMessage?.Invoke($"  No user members found in group '{groupName}' to sample for permission check (after filtering non-users)."); // *** FIX: Use OnOutputMessage event ***
                                            result.TargetGroupPermissions[groupName] = false; // No members to check, consider it false
                                        }
                                        else
                                        {
                                            bool anySampledUserHasAclPermission = false;
                                            OnOutputMessage?.Invoke($"  Checking 'Reset Password' ACL for {membersToSample.Count} random user(s) in group '{groupName}'."); // *** FIX: Use OnOutputMessage event ***

                                            foreach (string memberDn in membersToSample)
                                            {
                                                // Create a new DirectoryEntry with credentials for each member, for ACL check purposes
                                                using (DirectoryEntry memberEntry = new DirectoryEntry($"LDAP://{memberDn}", username, password)) // *** FIX: Use method parameters ***
                                                {
                                                    // Ensure it's actually a user object for permission check
                                                    if (memberEntry.SchemaClassName.Equals("user", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        string memberSamAccountName = memberEntry.Properties["sAMAccountName"]?.Value?.ToString() ?? "N/A";
                                                        OnOutputMessage?.Invoke($"    Checking ACL for user: {memberSamAccountName} (DN: {memberDn})"); // *** FIX: Use OnOutputMessage event ***

                                                        bool canReset = CheckResetPasswordPermission(memberEntry, userSids); // The ACL-based check using ResetPasswordGuid
                                                        OnOutputMessage?.Invoke($"      '{username}' (or a group he's in) can reset password for '{memberSamAccountName}': {(canReset ? "YES" : "NO")}"); // *** FIX: Use OnOutputMessage event ***

                                                        if (canReset)
                                                        {
                                                            anySampledUserHasAclPermission = true;
                                                            break; // If one passes, we can conclude for the group and move on
                                                        }
                                                    }
                                                    else
                                                    {
                                                        OnOutputMessage?.Invoke($"    Skipping non-user member: {memberDn} (Schema: {memberEntry.SchemaClassName})"); // *** FIX: Use OnOutputMessage event ***
                                                    }
                                                }
                                            }

                                            // Final summary for the group based on ACL check
                                            if (anySampledUserHasAclPermission)
                                            {
                                                OnOutputMessage?.Invoke($"\nConclusion for group '{groupName}': User '{username}' has explicit 'Reset Password' ACL permission for sampled members."); // *** FIX: Use OnOutputMessage event ***
                                                result.TargetGroupPermissions[groupName] = true;
                                            }
                                            else
                                            {
                                                OnOutputMessage?.Invoke($"\nConclusion for group '{groupName}': User '{username}' DOES NOT have explicit 'Reset Password' ACL permission for sampled members."); // *** FIX: Use OnOutputMessage event ***
                                                OnOutputMessage?.Invoke("  Please change the user. This one doesn't have enough permission based on explicit ACLs."); // *** FIX: Use OnOutputMessage event ***
                                                result.TargetGroupPermissions[groupName] = false;
                                                result.ErrorMessage += $"\nUser lacks permission for group '{groupName}'."; // Accumulate errors
                                            }
                                        }
                                    }
                                    else
                                    {
                                        OnOutputMessage?.Invoke($"  Group '{groupName}' has no members or members could not be enumerated."); // *** FIX: Use OnOutputMessage event ***
                                        result.TargetGroupPermissions[groupName] = false;
                                    }
                                } // End using targetGroupEntry
                            } // End foreach groupName
                        } // End using userEntry
                    } // End using domainRootEntry
                }); // End Task.Run
                result.IsSuccessful = true; // Only set to true if no exceptions occurred up to this point
            }
            catch (DirectoryServicesCOMException dse)
            {
                string errorMessage = $"AD Error: {dse.Message} (Error Code: 0x{dse.ErrorCode:X})\nPossible causes: Invalid credentials, insufficient permissions to read AD objects, or domain unreachable. Ensure the user has at least read permissions on target objects.";
                OnOutputMessage?.Invoke(errorMessage);
                OnErrorOccurred?.Invoke(errorMessage);
                result.IsSuccessful = false;
                result.ErrorMessage = errorMessage;
            }
            catch (Exception ex)
            {
                string errorMessage = $"An unexpected error occurred: {ex.Message}";
                OnOutputMessage?.Invoke(errorMessage);
                OnErrorOccurred?.Invoke(errorMessage);
                System.Diagnostics.Debug.WriteLine($"Unhandled exception in RunPermissionCheck: {ex.ToString()}");
                result.IsSuccessful = false;
                result.ErrorMessage = errorMessage;
            }
            finally
            {
                OnOutputMessage?.Invoke($"\nPermission check for '{username}' completed.");
            }
            return result;
        }

        private static DirectoryEntry FindADObject(DirectoryEntry domainRootEntry, string sAMAccountName, string objectCategory)
        {
            using (DirectorySearcher searcher = new(domainRootEntry))
            {
                searcher.Filter = $"(&(objectCategory={objectCategory})(sAMAccountName={sAMAccountName}))";
                searcher.PropertiesToLoad.Add("distinguishedName");
                searcher.PropertiesToLoad.Add("sAMAccountName");
                searcher.SearchScope = SearchScope.Subtree;

                SearchResult result = searcher.FindOne();
                return result?.GetDirectoryEntry();
            }
        }

        /// <summary>
        /// Gets the Distinguished Names of all direct members of a given group.
        /// </summary>
        private static List<string> GetGroupMembers(DirectoryEntry groupEntry)
        {
            List<string> members = [];
            try
            {
                object membersCollectionObject = groupEntry.Invoke("Members");

                if (membersCollectionObject is not IEnumerable enumerableMembers)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: 'Invoke(\"Members\")' result is not enumerable for group '{groupEntry.Name}'. It returned type: {membersCollectionObject?.GetType().FullName ?? "null"}");
                    return members;
                }

                foreach (object memberComObject in enumerableMembers)
                {
                    using (DirectoryEntry memberEntry = new DirectoryEntry(memberComObject))
                    {
                        if (memberEntry.Properties.Contains("distinguishedName"))
                        {
                            members.Add(memberEntry.Properties["distinguishedName"].Value.ToString());
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Member '{memberEntry.Name}' in group '{groupEntry.Name}' does not have a distinguishedName property.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting members for group '{groupEntry.Name}': {ex.Message}");
            }
            return members;
        }

        /// <summary>
        /// Recursively gets all SIDs (user's own and all groups) for a given user.
        /// Passes the domain name directly to PrincipalContext.
        /// </summary>
        private static HashSet<SecurityIdentifier> GetAllUserGroupSids(DirectoryEntry userEntry, string domainName, string username, string password)
        {
            HashSet<SecurityIdentifier> sids = new HashSet<SecurityIdentifier>();
            try
            {
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domainName, username, password))
                {
                    // Use userEntry's sAMAccountName property, not the passed username parameter,
                    // as the parameter 'username' might be a display name or UPN, while sAMAccountName is reliable for FindByIdentity
                    UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, userEntry.Properties["sAMAccountName"].Value.ToString());

                    if (userPrincipal != null)
                    {
                        foreach (Principal group in userPrincipal.GetAuthorizationGroups())
                        {
                            if (group.Sid != null)
                            {
                                sids.Add(group.Sid);
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"UserPrincipal for '{username}' not found in domain '{domainName}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating group SIDs for '{username}' in domain '{domainName}': {ex.Message}");
            }
            return sids;
        }

        /// <summary>
        /// Checks if a user (or their group memberships) has the specific "Reset Password" extended right,
        /// "All Extended Rights", OR "WriteProperty" on the unicodePwd attribute on the target user entry based on its ACL.
        /// </summary>
        private static bool CheckResetPasswordPermission(DirectoryEntry targetUserEntry, HashSet<SecurityIdentifier> userSids)
        {
            try
            {
                targetUserEntry.RefreshCache(["nTSecurityDescriptor"]); // Request the security descriptor explicitly
                ActiveDirectorySecurity security = targetUserEntry.ObjectSecurity;

                foreach (ActiveDirectoryAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType == AccessControlType.Allow)
                    {
                        // Check if the rule's identity reference is one of the user's or their group's SIDs
                        if (userSids.Contains(rule.IdentityReference as SecurityIdentifier))
                        {
                            // Condition 1: Specific "Reset Password" Extended Right
                            bool isResetPasswordExtendedRight =
                                rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.ExtendedRight) &&
                                rule.ObjectType.Equals(ResetPasswordGuid);

                            // Condition 2: "All Extended Rights" (ObjectType is Guid.Empty for "All Extended Rights")
                            bool isAllExtendedRights =
                                rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.ExtendedRight) &&
                                rule.ObjectType.Equals(Guid.Empty);

                            // Condition 3: Write permission on the 'unicodePwd' attribute
                            bool isWriteUnicodePwd =
                                rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.WriteProperty) &&
                                rule.ObjectType.Equals(UnicodePwdGuid);

                            // You might also consider GenericAll, GenericWrite, WriteDacl, WriteOwner, though less specific.
                            // For simplicity, sticking to the primary ones for password reset.
                            // If you need to check for inherited permissions or block inheritance, that's more complex.

                            if (isResetPasswordExtendedRight || isAllExtendedRights || isWriteUnicodePwd)
                            {
                                return true; // Found an explicit ALLOW rule for Reset Password in one of its common forms
                            }
                        }
                    }
                }
            }
            catch (DirectoryServicesCOMException dse)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading security for {targetUserEntry.Name}: {dse.Message} (Code: 0x{dse.ErrorCode:X})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General error during permission check for {targetUserEntry.Name}: {ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Represents the result of a permission check operation.
    /// </summary>
    public class PermissionCheckResult
    {
        public bool IsSuccessful { get; set; } = false; // Default to false
        public string ErrorMessage { get; set; } = string.Empty;
        public bool HasFullPermission { get; set; } = false; // True if highly privileged
        public bool IsHighlyPrivileged { get; set; } = false; // Indicates if the user is in Admin groups
        // Dictionary to store permission status for each target group
        public Dictionary<string, bool> TargetGroupPermissions { get; set; } = new Dictionary<string, bool>();
    }
}