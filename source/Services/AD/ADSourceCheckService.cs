using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Linq;
using System.Security.AccessControl;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services.AD
{
    public class ADSourceCheckService
    {
        public event Action<string>? OnOutputMessage;

        private static readonly Guid ResetPasswordGuid = new("00299570-246d-11d0-a768-00aa006e0529");
        private static readonly Guid UnicodePwdGuid = new("bf967a0a-0de6-11d0-a285-00aa003049e2");

        public async Task<PermissionCheckResult> RunPermissionCheckAsync(string domain, string username, string password, List<string> targetGroupNames)
        {
            var result = new PermissionCheckResult();
            bool isUserHighlyPrivileged = false;

            OnOutputMessage?.Invoke($"Starting permission check for user '{username}' on domain '{domain}'...");

            try
            {
                await Task.Run(() =>
                {
                    OnOutputMessage?.Invoke($"Attempting to ping domain controller at '{domain}'...");
                    if (!IsDomainReachable(domain))
                    {
                        OnOutputMessage?.Invoke($"Warning: Domain controller for '{domain}' is not reachable via Ping. This might be due to firewall rules, but we will still attempt LDAP connectivity.");
                    }
                    else
                    {
                        OnOutputMessage?.Invoke($"Successfully pinged domain controller at '{domain}'.");
                    }

                    DirectoryEntry domainRootEntry;
                    try
                    {
                        OnOutputMessage?.Invoke($"Attempting to bind to domain '{domain}' with user '{username}' via LDAP...");
                        domainRootEntry = new DirectoryEntry($"LDAP://{domain}", username, password)
                        {
                            AuthenticationType = AuthenticationTypes.Secure
                        };
                        string? dn = domainRootEntry.Properties["distinguishedName"].Value?.ToString();
                        domainRootEntry.RefreshCache();
                        OnOutputMessage?.Invoke($"Successfully bound to domain root: {dn ?? "N/A"} with provided credentials.");
                    }
                    catch (DirectoryServicesCOMException dse)
                    {
                        string msg;
                        if (dse.ErrorCode == -2147023570)
                        {
                            msg = $"Error: Authentication failed for user '{username}' on domain '{domain}'. Please check the username and password. (AD Error Code: 0x{dse.ErrorCode:X})";
                        }
                        else if (dse.ErrorCode == -2147016646)
                        {
                            msg = $"Error: The Active Directory server for '{domain}' is not operational or cannot be contacted on LDAP ports (e.g., 389/636), even if ping responded. Check DC health and firewall rules. (AD Error Code: 0x{dse.ErrorCode:X})";
                        }
                        else
                        {
                            msg = $"Error: AD Binding Error: {dse.Message} (Error Code: 0x{dse.ErrorCode:X})\nPossible causes: General AD connectivity, DNS issues for SRV records, or insufficient permissions for initial bind.";
                        }
                        OnOutputMessage?.Invoke(msg);
                        result.IsSuccessful = false;
                        result.ErrorMessage = msg;
                        return; // IMPORTANT: Exit here if initial bind fails
                    }
                    catch (Exception ex)
                    {
                        string msg = $"Error: An unexpected error occurred during initial AD bind: {ex.Message}";
                        OnOutputMessage?.Invoke(msg);
                        result.IsSuccessful = false;
                        result.ErrorMessage = msg;
                        return; // IMPORTANT: Exit here if initial bind fails
                    }

                    // If we reach here, initial bind was successful. Now proceed with user/group checks.
                    // Set IsSuccessful to true initially, and only set to false if subsequent checks fail.
                    result.IsSuccessful = true;


                    using DirectoryEntry? userEntry = FindADObject(domainRootEntry, username, "user");
                    {
                        if (userEntry == null)
                        {
                            string msg = $"Error: User '{username}' not found in domain '{domain}'. Ensure the username is correct and the account exists.";
                            OnOutputMessage?.Invoke(msg);
                            result.IsSuccessful = false;
                            result.ErrorMessage = msg;
                            return;
                        }
                        OnOutputMessage?.Invoke($"User '{username}' object found (DN: {userEntry.Properties["distinguishedName"].Value?.ToString() ?? "N/A"}).");

                        HashSet<SecurityIdentifier> userSids;
                        try
                        {
                            OnOutputMessage?.Invoke($"Enumerating group memberships for '{username}'...");
                            userSids = GetAllUserGroupSids(userEntry, domain, username, password);
                            if (userSids.Count == 0)
                            {
                                OnOutputMessage?.Invoke($"Warning: No group SIDs found for user '{username}'. This might indicate a problem or the user has no group memberships.");
                            }
                            else
                            {
                                OnOutputMessage?.Invoke($"Found {userSids.Count} SIDs for '{username}' (including self and groups).");
                            }
                            foreach (var sid in userSids)
                            {
                                try
                                {
                                    string name = sid.Translate(typeof(NTAccount)).ToString();
                                    OnOutputMessage?.Invoke($"  - SID: {sid.Value} ({name})");
                                }
                                catch (IdentityNotMappedException)
                                {
                                    OnOutputMessage?.Invoke($"  - SID: {sid.Value} (Cannot map to NTAccount - e.g., WellKnown SID or foreign domain)");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            string msg = $"Error: Error enumerating group memberships for '{username}': {ex.Message}\nThis might indicate insufficient permissions for the user '{username}' to read its own group memberships, or a problem with the domain controller.";
                            OnOutputMessage?.Invoke(msg);
                            result.IsSuccessful = false;
                            result.ErrorMessage = msg;
                            return;
                        }

                        List<string> privilegedGroupMemberships = [];

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

                        isUserHighlyPrivileged = privilegedGroupMemberships.Count != 0;
                        result.IsHighlyPrivileged = isUserHighlyPrivileged;

                        if (isUserHighlyPrivileged)
                        {
                            string groupsList = string.Join(", ", privilegedGroupMemberships);
                            OnOutputMessage?.Invoke($"\nUser '{username}' IS a member of a highly privileged administrative group(s): {groupsList}.");
                            OnOutputMessage?.Invoke("  Assuming full password reset permissions for all target groups.");
                            result.HasFullPermission = true;
                        }
                        else
                        {
                            OnOutputMessage?.Invoke($"\nUser '{username}' is NOT a member of recognized highly privileged administrative groups.");
                            OnOutputMessage?.Invoke("  Proceeding with specific ACL checks for 'Reset Password' extended right.");
                        }

                        foreach (string groupName in targetGroupNames)
                        {
                            OnOutputMessage?.Invoke($"\n--- Processing Target Group: {groupName} ---");
                            using DirectoryEntry? targetGroupEntry = FindADObject(domainRootEntry, groupName, "group");
                            {
                                if (targetGroupEntry == null)
                                {
                                    OnOutputMessage?.Invoke($"  Warning: Target group '{groupName}' not found in domain '{domain}'. Skipping this group.");
                                    result.TargetGroupPermissions[groupName] = false;
                                    result.IsSuccessful = false; // A specific target group not found should fail the overall check
                                    result.ErrorMessage += (string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : "\n") + $"Target group '{groupName}' not found.";
                                    continue;
                                }
                                OnOutputMessage?.Invoke($"  Found target group: {targetGroupEntry.Properties["distinguishedName"].Value?.ToString() ?? "N/A"}");

                                if (isUserHighlyPrivileged)
                                {
                                    OnOutputMessage?.Invoke($"  -> Conclusion for group '{groupName}': User '{username}' (highly privileged admin) has assumed permission to reset passwords for its members.");
                                    result.TargetGroupPermissions[groupName] = true;
                                    continue;
                                }

                                List<string> memberDns = GetGroupMembers(targetGroupEntry);
                                if (memberDns.Count != 0)
                                {
                                    OnOutputMessage?.Invoke($"  Found {memberDns.Count} members in group '{groupName}'.");
                                    const int sampleSize = 2;
                                    Random rand = new();
                                    List<string> membersToSample = [.. memberDns.OrderBy(x => rand.Next()).Take(sampleSize)];

                                    if (membersToSample.Count == 0)
                                    {
                                        OnOutputMessage?.Invoke($"  No user members found in group '{groupName}' to sample for permission check (after filtering non-users).");
                                        result.TargetGroupPermissions[groupName] = false;
                                        result.IsSuccessful = false; // No user members to check means we can't confirm permissions
                                        result.ErrorMessage += (string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : "\n") + $"No user members found in group '{groupName}' for permission check.";
                                    }
                                    else
                                    {
                                        bool anySampledUserHasAclPermission = false;
                                        OnOutputMessage?.Invoke($"  Checking 'Reset Password' ACL for {membersToSample.Count} random user(s) in group '{groupName}'.");

                                        foreach (string memberDn in membersToSample)
                                        {
                                            using DirectoryEntry memberEntry = new($"LDAP://{memberDn}", username, password);
                                            {
                                                if (memberEntry.SchemaClassName.Equals("user", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    string memberSamAccountName = memberEntry.Properties["sAMAccountName"]?.Value?.ToString() ?? "N/A";
                                                    OnOutputMessage?.Invoke($"    Checking ACL for user: {memberSamAccountName} (DN: {memberDn})");

                                                    bool canReset = CheckResetPasswordPermission(memberEntry, userSids);
                                                    OnOutputMessage?.Invoke($"      '{username}' (or a group he's in) can reset password for '{memberSamAccountName}': {(canReset ? "YES" : "NO")}");

                                                    if (canReset)
                                                    {
                                                        anySampledUserHasAclPermission = true;
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    OnOutputMessage?.Invoke($"    Skipping non-user member: {memberDn} (Schema: {memberEntry.SchemaClassName})");
                                                }
                                            }
                                        }

                                        if (anySampledUserHasAclPermission)
                                        {
                                            OnOutputMessage?.Invoke($"\nConclusion for group '{groupName}': User '{username}' has explicit 'Reset Password' ACL permission for sampled members.");
                                            result.TargetGroupPermissions[groupName] = true;
                                        }
                                        else
                                        {
                                            OnOutputMessage?.Invoke($"\nConclusion for group '{groupName}': User '{username}' DOES NOT have explicit 'Reset Password' ACL permission for sampled members.");
                                            OnOutputMessage?.Invoke("  Please change the user. This one doesn't have enough permission based on explicit ACLs.");
                                            result.TargetGroupPermissions[groupName] = false;
                                            result.IsSuccessful = false; // Explicit ACL check failed for sampled members, so overall check fails
                                            result.ErrorMessage += (string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : "\n") + $"User lacks permission for group '{groupName}'.";
                                        }
                                    }
                                }
                                else
                                {
                                    OnOutputMessage?.Invoke($"  Group '{groupName}' has no members or members could not be enumerated.");
                                    result.TargetGroupPermissions[groupName] = false;
                                    result.IsSuccessful = false; // No members or couldn't enumerate means we can't confirm permissions
                                    result.ErrorMessage += (string.IsNullOrWhiteSpace(result.ErrorMessage) ? "" : "\n") + $"Could not enumerate members for group '{groupName}'.";
                                }
                            }
                        }
                    }
                });

                // Final check to ensure IsSuccessful is false if any error message was set
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result.IsSuccessful = false;
                }
                // Removed the redundant if/else if/else block here, as IsSuccessful is set throughout the process
                // and a final check against ErrorMessage ensures correctness.

            }
            catch (Exception ex)
            {
                string errorMessage = $"Error: An unexpected error occurred during permission check: {ex.Message}";
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

        private bool IsDomainReachable(string domain)
        {
            try
            {
                using Ping pingSender = new();
                PingReply reply = pingSender.Send(domain, 2000);
                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }
                else
                {
                    OnOutputMessage?.Invoke($"Info: Ping to '{domain}' failed with status: {reply.Status}.");
                    return false;
                }
            }
            catch (PingException ex)
            {
                OnOutputMessage?.Invoke($"Info: Ping to '{domain}' failed: {ex.Message}.");
                return false;
            }
            catch (Exception ex)
            {
                OnOutputMessage?.Invoke($"Info: An unexpected error occurred during ping check for '{domain}': {ex.Message}.");
                return false;
            }
        }
        private static DirectoryEntry? FindADObject(DirectoryEntry domainRootEntry, string sAMAccountName, string objectCategory)
        {
            using DirectorySearcher searcher = new(domainRootEntry);
            searcher.Filter = $"(&(objectCategory={objectCategory})(sAMAccountName={sAMAccountName}))";
            searcher.PropertiesToLoad.Add("distinguishedName");
            searcher.PropertiesToLoad.Add("sAMAccountName");
            searcher.SearchScope = SearchScope.Subtree;

            SearchResult? result = searcher.FindOne();
            return result?.GetDirectoryEntry();
        }

        private static List<string> GetGroupMembers(DirectoryEntry groupEntry)
        {
            List<string> members = [];
            try
            {
                object? membersCollectionObject = groupEntry.Invoke("Members");

                if (membersCollectionObject is not IEnumerable enumerableMembers)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: 'Invoke(\"Members\")' result is not enumerable for group '{groupEntry.Name}'. It returned type: {membersCollectionObject?.GetType().FullName ?? "null"}");
                    return members;
                }

                foreach (object memberComObject in enumerableMembers)
                {
                    using DirectoryEntry memberEntry = new(memberComObject);
                    if (memberEntry.Properties.Contains("distinguishedName"))
                    {
                        string? dn = memberEntry.Properties["distinguishedName"].Value?.ToString();
                        if (dn != null)
                        {
                            members.Add(dn);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Member '{memberEntry.Name}' in group '{groupEntry.Name}' has a distinguishedName property that is null.");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Member '{memberEntry.Name}' in group '{groupEntry.Name}' does not have a distinguishedName property.");
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
            HashSet<SecurityIdentifier> sids = [];
            try
            {
                using PrincipalContext context = new(ContextType.Domain, domainName, username, password);
                string? samAccountName = userEntry.Properties["sAMAccountName"]?.Value?.ToString();
                if (string.IsNullOrEmpty(samAccountName))
                {
                    System.Diagnostics.Debug.WriteLine($"UserEntry for '{username}' has no sAMAccountName. Cannot fetch group SIDs.");
                    return sids;
                }

                using UserPrincipal? userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, samAccountName);

                if (userPrincipal != null)
                {
                    foreach (Principal group in userPrincipal.GetAuthorizationGroups())
                    {
                        if (group.Sid != null)
                        {
                            sids.Add(group.Sid);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Group '{group.Name}' has a null SID.");
                        }
                        group.Dispose();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"UserPrincipal for '{username}' not found in domain '{domainName}'. This might indicate a problem with the user object or connectivity for the PrincipalContext.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating group SIDs for '{username}' in domain '{domainName}': {ex.Message}");
                throw;
            }
            return sids;
        }

        private static bool CheckResetPasswordPermission(DirectoryEntry targetUserEntry, HashSet<SecurityIdentifier> userSids)
        {
            try
            {
                targetUserEntry.RefreshCache(["nTSecurityDescriptor"]);
                ActiveDirectorySecurity security = targetUserEntry.ObjectSecurity;

                foreach (ActiveDirectoryAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    if (rule.AccessControlType == AccessControlType.Allow)
                    {
                        if (rule.IdentityReference is SecurityIdentifier ruleSid && userSids.Contains(ruleSid))
                        {
                            bool isResetPasswordExtendedRight =
                                rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.ExtendedRight) &&
                                rule.ObjectType.Equals(ResetPasswordGuid);

                            bool isAllExtendedRights =
                                rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.ExtendedRight) &&
                                rule.ObjectType.Equals(Guid.Empty);

                            bool isWriteUnicodePwd =
                                rule.ActiveDirectoryRights.HasFlag(ActiveDirectoryRights.WriteProperty) &&
                                rule.ObjectType.Equals(UnicodePwdGuid);

                            if (isResetPasswordExtendedRight || isAllExtendedRights || isWriteUnicodePwd)
                            {
                                return true;
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

    public class PermissionCheckResult
    {
        public bool IsSuccessful { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool HasFullPermission { get; set; } = false;
        public bool IsHighlyPrivileged { get; set; } = false;
        public Dictionary<string, bool> TargetGroupPermissions { get; set; } = [];
    }
}