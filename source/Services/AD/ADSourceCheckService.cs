// File: Services.AD/ADSourceCheckService.cs
using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services; // Assuming ILoggingService and ISourceCheckService are here
using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices; // For Marshal
using System.Security; // For SecureString
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services.AD
{
    public class ADSourceCheckService : IADSourceCheckService // Corrected class name and interface
    {
        private readonly ILoggingService _logger;

        public event Action<string>? OnOutputMessage;

        private static readonly Guid ResetPasswordGuid = new("00299570-246d-11d0-a768-00aa006e0529");
        private static readonly Guid UnicodePwdGuid = new("bf967a0a-0de6-11d0-a285-00aa003049e2");
        private const int MemberSampleSize = 2;

        public ADSourceCheckService(ILoggingService logger)
        {
            _logger = logger;
        }

        private void LogAndOutput(string message, LogLevel level = LogLevel.Info)
        {
            _logger.Log(message, level);
            OnOutputMessage?.Invoke(message);
        }

        public async Task<PermissionCheckResult> RunPermissionCheckAsync(string domain, string username, SecureString password, List<string> targetGroups)
        {
            var result = new PermissionCheckResult();
            LogAndOutput($"Starting permission check for user '{username}' on domain '{domain}'...");

            IntPtr passwordPtr = IntPtr.Zero;
            string? plaintextPassword = null;

            try
            {
                passwordPtr = Marshal.SecureStringToBSTR(password);
                plaintextPassword = Marshal.PtrToStringBSTR(passwordPtr);

                await Task.Run(() =>
                {
                    if (!TryInitialConnection(domain, username, plaintextPassword, result, out var domainRootEntry, out var userEntry))
                    {
                        return;
                    }

                    result.IsSuccessful = true;

                    if (!CheckUserPrivileges(userEntry!, domain, username, plaintextPassword, result, out var userSids))
                    {
                        domainRootEntry?.Dispose();
                        userEntry?.Dispose();
                        return;
                    }

                    ProcessTargetGroups(domainRootEntry!, username, plaintextPassword, targetGroups, userSids, result);

                    domainRootEntry?.Dispose();
                    userEntry?.Dispose();
                });

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    result.IsSuccessful = false;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error: A critical unhandled error occurred during permission check: {ex.Message}";
                LogAndOutput(errorMessage, LogLevel.Error);
                System.Diagnostics.Debug.WriteLine($"Unhandled exception in RunPermissionCheck: {ex}");
                result.IsSuccessful = false;
                result.ErrorMessage = errorMessage;
            }
            finally
            {
                if (passwordPtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(passwordPtr);
                }
                plaintextPassword = null;
                LogAndOutput($"\nPermission check for '{username}' completed.");
            }
            return result;
        }

        private bool TryInitialConnection(string domain, string username, string? password, PermissionCheckResult result, out DirectoryEntry? domainRootEntry, out DirectoryEntry? userEntry)
        {
            domainRootEntry = null;
            userEntry = null;

            LogAndOutput($"\n--- Phase 1: Domain and User Validation ---");
            LogAndOutput($"Attempting to ping domain controller at '{domain}'...");
            if (!IsDomainReachable(domain))
            {
                LogAndOutput($"Warning: Domain controller for '{domain}' is not reachable via Ping. Proceeding with LDAP connection attempt...", LogLevel.Warning);
            }
            else
            {
                LogAndOutput($"Successfully pinged domain controller at '{domain}'.");
            }

            try
            {
                LogAndOutput($"Attempting to bind to domain '{domain}' with user '{username}'...");
                domainRootEntry = new DirectoryEntry($"LDAP://{domain}", username, password ?? "", AuthenticationTypes.Secure);
                LogAndOutput($"Successfully bound to domain root: {domainRootEntry.Properties["distinguishedName"].Value ?? "N/A"}.");
            }
            catch (DirectoryServicesCOMException dse)
            {
                string msg = dse.ErrorCode switch
                {
                    -2147023570 => $"Error: Authentication failed for user '{username}'. Please check credentials. (Code: 0x{dse.ErrorCode:X})",
                    -2147016646 => $"Error: The AD server for '{domain}' is not operational or firewalled. (Code: 0x{dse.ErrorCode:X})",
                    _ => $"Error: AD Binding Error: {dse.Message} (Code: 0x{dse.ErrorCode:X})",
                };
                LogAndOutput(msg, LogLevel.Error);
                result.ErrorMessage = msg;
                domainRootEntry?.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                LogAndOutput(ex.ToString(), LogLevel.Error);
                return false;
            }

            userEntry = FindADObject(domainRootEntry, username, "user");
            if (userEntry == null)
            {
                string msg = $"Error: User '{username}' not found in domain '{domain}'.";
                LogAndOutput(msg, LogLevel.Error);
                result.ErrorMessage = msg;
                domainRootEntry?.Dispose();
                return false;
            }

            LogAndOutput($"User object '{username}' found (DN: {userEntry.Properties["distinguishedName"].Value}).");
            return true;
        }

        private bool CheckUserPrivileges(DirectoryEntry userEntry, string domain, string username, string? password, PermissionCheckResult result, out HashSet<SecurityIdentifier> userSids)
        {
            userSids = new HashSet<SecurityIdentifier>();
            LogAndOutput($"\n--- Phase 2: User Privilege Analysis ---");

            try
            {
                LogAndOutput($"Enumerating group memberships for '{username}'...");
                userSids = GetAllUserGroupSids(userEntry, domain, username, password ?? "");
                LogAndOutput($"Found {userSids.Count} total security group memberships (SIDs).");
            }
            catch (Exception ex)
            {
                string msg = $"Error enumerating group memberships: {ex.Message}";
                LogAndOutput(msg, LogLevel.Error);
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
                LogAndOutput($"User IS a member of highly-privileged group(s): {groupsList}.");
                LogAndOutput("--> Assuming full permissions for all target groups.");
            }
            else
            {
                LogAndOutput("User is NOT a member of highly-privileged groups.");
                LogAndOutput("--> Proceeding with specific ACL checks.");
            }
            return true;
        }

        private void ProcessTargetGroups(DirectoryEntry domainRootEntry, string username, string? password, List<string> targetGroupNames, HashSet<SecurityIdentifier> userSids, PermissionCheckResult result)
        {
            LogAndOutput($"\n--- Phase 3: Target Group Permission Checks ---");
            foreach (string groupName in targetGroupNames)
            {
                CheckPermissionsForSingleGroup(domainRootEntry, username, password, groupName, userSids, result);
            }
        }

        private void CheckPermissionsForSingleGroup(DirectoryEntry domainRootEntry, string username, string? password, string groupName, HashSet<SecurityIdentifier> userSids, PermissionCheckResult result)
        {
            LogAndOutput($"\n--- Processing Target Group: {groupName} ---");
            using var targetGroupEntry = FindADObject(domainRootEntry, groupName, "group");

            if (targetGroupEntry == null)
            {
                LogAndOutput($"  Warning: Target group '{groupName}' not found. Skipping.", LogLevel.Warning);
                result.TargetGroupPermissions[groupName] = false;
                if (string.IsNullOrWhiteSpace(result.ErrorMessage)) { result.ErrorMessage = ""; }
                result.ErrorMessage += $"\nTarget group '{groupName}' not found.";
                return;
            }

            LogAndOutput($"  Found target group: {targetGroupEntry.Properties["distinguishedName"].Value}");

            if (result.IsHighlyPrivileged)
            {
                LogAndOutput($"  -> Conclusion: User has assumed permissions (is Admin).");
                result.TargetGroupPermissions[groupName] = true;
                return;
            }

            List<string> memberDns = GetGroupMembers(targetGroupEntry);
            if (memberDns.Count == 0)
            {
                LogAndOutput($"  Warning: Group '{groupName}' has no members or they could not be enumerated.", LogLevel.Warning);
                result.TargetGroupPermissions[groupName] = false;
                if (string.IsNullOrWhiteSpace(result.ErrorMessage)) { result.ErrorMessage = ""; }
                result.ErrorMessage += $"\nNo members found to check in group '{groupName}'.";
                return;
            }

            LogAndOutput($"  Found {memberDns.Count} members. Sampling up to {MemberSampleSize} to check ACLs.");
            bool hasPermission = VerifyAclOnSampledMembers(domainRootEntry, memberDns, username, password, userSids);

            if (hasPermission)
            {
                LogAndOutput($"  -> Conclusion for '{groupName}': YES, user has 'Reset Password' permission for sampled members.");
                result.TargetGroupPermissions[groupName] = true;
            }
            else
            {
                LogAndOutput($"  -> Conclusion for '{groupName}': NO, user does not have permission for sampled members.", LogLevel.Warning);
                result.TargetGroupPermissions[groupName] = false;
                if (string.IsNullOrWhiteSpace(result.ErrorMessage)) { result.ErrorMessage = ""; }
                result.ErrorMessage += $"\nUser lacks permission for group '{groupName}'.";
            }
        }

        private bool VerifyAclOnSampledMembers(DirectoryEntry domainRootEntry, List<string> memberDns, string username, string? password, HashSet<SecurityIdentifier> userSids)
        {
            var rand = new Random();
            var membersToSample = memberDns.OrderBy(x => rand.Next()).Take(MemberSampleSize).ToList();

            foreach (string memberDn in membersToSample)
            {
                try
                {
                    using var searcher = new DirectorySearcher(domainRootEntry)
                    {
                        Filter = $"(distinguishedName={EscapeLdapFilter(memberDn)})",
                        SearchScope = SearchScope.Subtree
                    };
                    searcher.PropertiesToLoad.Add("sAMAccountName");

                    SearchResult? searchResult = searcher.FindOne();

                    if (searchResult != null)
                    {
                        using DirectoryEntry memberEntry = searchResult.GetDirectoryEntry();
                        string memberSam = memberEntry.Properties["sAMAccountName"]?.Value?.ToString() ?? "N/A";
                        LogAndOutput($"    Checking ACL for user: {memberSam} (DN: {memberDn})");

                        if (CheckResetPasswordPermission(memberEntry, userSids))
                        {
                            LogAndOutput($"      Permission found for {memberSam}.");
                            return true;
                        }
                    }
                    else
                    {
                        LogAndOutput($"    Warning: Could not find member object '{memberDn}' via search. It may have been deleted. Skipping.", LogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogAndOutput($"    Warning: An error occurred while searching for member '{memberDn}'. Skipping. Error: {ex.Message}", LogLevel.Warning);
                }
            }
            return false;
        }

        private static string EscapeLdapFilter(string value)
        {
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
                LogAndOutput($"Info: Ping to '{domain}' failed: {ex.Message}.", LogLevel.Info);
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
                throw;
            }
            return sids;
        }

        private static bool CheckResetPasswordPermission(DirectoryEntry targetUserEntry, HashSet<SecurityIdentifier> userSids)
        {
            try
            {
                targetUserEntry.RefreshCache(["nTSecurityDescriptor"]);
                var security = (ActiveDirectorySecurity)targetUserEntry.ObjectSecurity;

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
}