// File: Services.AD/PasswordResetService.cs
using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services; // For ILoggingService, LogLevel, ICredentialStorageService, CredentialEntry
using System;
using System.DirectoryServices.AccountManagement; // For AD interaction
using System.Globalization; // Needed for CultureInfo
using System.Linq; // For .Any() and .FirstOrDefault()
using System.Security; // For SecureString
using System.Runtime.InteropServices; // For SecureString to string conversion (Marshal)

namespace AD_User_Reset_Print.Services.AD
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly ILoggingService _logger; // Depend on the interface
        private readonly ICredentialStorageService _credentialStorageService; // Depend on the interface

        public PasswordResetService(ILoggingService logger, ICredentialStorageService credentialStorageService)
        {
            _logger = logger;
            _credentialStorageService = credentialStorageService;
        }

        /// <summary>
        /// Resets a user's password in Active Directory using configured administrative credentials
        /// and returns the generated temporary password.
        /// </summary>
        /// <param name="user">The User object for whom to reset the password.</param>
        /// <returns>The newly generated temporary password, or null if the reset failed.</returns>
        public string? Reset(User user) // Changed return type to string? for clarity
        {
            if (user == null)
            {
                _logger.Log("Password reset called but no user was selected.", LogLevel.Warning);
                return null;
            }

            // 1. Load credentials from storage
            List<CredentialEntry> credentials = _credentialStorageService.LoadCredentials();

            if (credentials.Count == 0)
            {
                _logger.Log("Password reset failed: No administrator credentials are configured.", LogLevel.Error);
                return null;
            }

            // 2. Select the appropriate admin credential
            // Use FirstOrDefault and null-check for safety, avoiding First() which throws if collection is empty
            var adminCredential = credentials.FirstOrDefault(c => string.Equals(c.Domain, user.Domain, StringComparison.OrdinalIgnoreCase));
            if (adminCredential == null)
            {
                adminCredential = credentials.First(); // Fallback to the first available if no domain-specific
                _logger.Log($"No specific credential found for domain '{user.Domain}'. Using fallback credential for '{adminCredential.Domain}'.", LogLevel.Warning);
            }

            // 3. Generate the temporary password
            string newTempPassword = GenerateTempPasswordForDate(DateTime.Today);

            IntPtr passwordPtr = IntPtr.Zero; // Pointer for SecureString conversion
            string? adminPasswordPlaintext = null; // Store plaintext password temporarily

            try
            {
                _logger.Log($"Attempting to reset password for user '{user.SAMAccountName}' in domain '{adminCredential.Domain}'.");

                // Convert SecureString to plaintext string
                // IMPORTANT: This exposes the password in memory. Minimize its lifetime.
                passwordPtr = Marshal.SecureStringToBSTR(adminCredential.Password);
                adminPasswordPlaintext = Marshal.PtrToStringBSTR(passwordPtr);

                // 4. Perform the Active Directory operation
                using (var context = new PrincipalContext(ContextType.Domain, adminCredential.Domain, adminCredential.Username, adminPasswordPlaintext))
                {
                    using (var userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, user.SAMAccountName))
                    {
                        if (userPrincipal == null)
                        {
                            _logger.Log($"User '{user.SAMAccountName}' could not be found in the domain '{adminCredential.Domain}'.", LogLevel.Error);
                            return null;
                        }

                        userPrincipal.SetPassword(newTempPassword);
                        userPrincipal.ExpirePasswordNow(); // Forces user to change password on next logon
                        userPrincipal.Save();

                        _logger.Log($"Successfully reset password for '{user.DisplayName}' ({user.SAMAccountName}).", LogLevel.Info);
                        return newTempPassword;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"An error occurred during password reset for '{user.SAMAccountName}'. Details: {ex.Message}", LogLevel.Error);
                System.Diagnostics.Debug.WriteLine($"Password Reset Exception: {ex}"); // For detailed debug output
                return null;
            }
            finally
            {
                // Crucially, zero out the plaintext password from memory
                if (passwordPtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(passwordPtr);
                }
                adminPasswordPlaintext = null; // Ensure the reference is cleared
            }
        }

        /// <summary>
        /// Retrieves the LastPasswordSet date for a user from Active Directory using specified administrative credentials.
        /// </summary>
        /// <param name="user">The user object to query.</param>
        /// <returns>The DateTime of the last password set, or null if not found or an error occurs.</returns>
        public DateTime? GetLastPasswordSetDate(User user)
        {
            if (user == null)
            {
                _logger.Log("GetLastPasswordSetDate called but no user was selected.", LogLevel.Warning);
                return null;
            }

            List<CredentialEntry> credentials = _credentialStorageService.LoadCredentials();
            if (credentials.Count == 0)
            {
                _logger.Log("Cannot get LastPasswordSet date: No administrator credentials are configured.", LogLevel.Error);
                return null;
            }

            // Use FirstOrDefault and null-check for safety
            var adminCredential = credentials.FirstOrDefault(c => string.Equals(c.Domain, user.Domain, StringComparison.OrdinalIgnoreCase));
            if (adminCredential == null)
            {
                adminCredential = credentials.First(); // Fallback
                _logger.Log($"No specific credential found for domain '{user.Domain}'. Using fallback credential for '{adminCredential.Domain}'.", LogLevel.Warning);
            }

            IntPtr passwordPtr = IntPtr.Zero;
            string? adminPasswordPlaintext = null;

            try
            {
                passwordPtr = Marshal.SecureStringToBSTR(adminCredential.Password);
                adminPasswordPlaintext = Marshal.PtrToStringBSTR(passwordPtr);

                using (var context = new PrincipalContext(ContextType.Domain, adminCredential.Domain, adminCredential.Username, adminPasswordPlaintext))
                {
                    using (var userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, user.SAMAccountName))
                    {
                        if (userPrincipal != null)
                        {
                            return userPrincipal.LastPasswordSet;
                        }
                        else
                        {
                            _logger.Log($"Could not find user '{user.SAMAccountName}' to get LastPasswordSet date.", LogLevel.Warning);
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to get LastPasswordSet date for '{user.SAMAccountName}'. Details: {ex.Message}", LogLevel.Error);
                System.Diagnostics.Debug.WriteLine($"GetLastPasswordSetDate Exception: {ex}");
                return null;
            }
            finally
            {
                if (passwordPtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(passwordPtr);
                }
                adminPasswordPlaintext = null;
            }
        }

        /// <summary>
        /// Generates a temporary password in the format "Gyre@DD.MM.YYYY" for a given date.
        /// </summary>
        public static string GenerateTempPasswordForDate(DateTime date)
        {
            string formattedDate = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            return $"Gyre@{formattedDate}";
        }
    }
}