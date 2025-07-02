using Microsoft.VisualStudio.TestTools.UnitTesting;
using AD_User_Reset_Print.Services.AD;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Models;
using System;
using System.Security;
using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Configuration;
using AD_User_Reset_Print.Tests.TestHelpers; // For configuration

namespace AD_User_Reset_Print.Tests.Integration.Services.AD
{
    [TestClass]
    public class PasswordResetServiceIntegrationTests
    {
        private ILoggingService? _logger;
        private ICredentialStorageService? _credentialStorageService;
        private PasswordResetService? _passwordResetService;
        private IConfiguration? _configuration;

        // Configuration values
        private string? _testDomain;
        private string? _adminUsername;
        private SecureString? _adminPassword;
        private string? _testUserToReset; // User that will have its password reset
        private string? _testUserToResetSAM; // SAMAccountName of the user
        private string? _initialUserPassword; // Initial password to set and then reset

        [TestInitialize]
        public void TestInitialize()
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.integrationtests.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            _testDomain = _configuration["ADSettings:Domain"];
            _adminUsername = _configuration["ADSettings:AdminUsername"];
            _testUserToReset = _configuration["ADSettings:TestUserToResetDN"]; // e.g., "CN=Test User,CN=Users,DC=yourtestdomain,DC=local"
            _testUserToResetSAM = _configuration["ADSettings:TestUserToResetSAM"]; // e.g., "testuser"
            _initialUserPassword = _configuration["ADSettings:InitialUserPassword"];

            _logger = new ConsoleLogger(); // Use a console logger for integration tests
            var mockCredentialStorage = new Moq.Mock<ICredentialStorageService>();
            mockCredentialStorage.Setup(cs => cs.LoadCredentials()).Returns(
            [
                new CredentialEntry(
                    _testDomain!,
                    _adminUsername!,
                    SecureStringToString(_adminPassword!), // password (converted from SecureString)
                    []
                )
            ]);
            _credentialStorageService = mockCredentialStorage.Object;
            _passwordResetService = new PasswordResetService(_logger, _credentialStorageService);

            // Ensure the test user exists and has a known initial password
            EnsureTestUserExistsAndSetInitialPassword(_testDomain!, _adminUsername!, _adminPassword!, _testUserToResetSAM!, _initialUserPassword!);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _adminPassword?.Dispose();
            // Optional: Reset password back to initial known password or disable user after test
            // You might want to do this in a finally block in the test itself or a dedicated cleanup method
        }

        private static SecureString ConvertToSecureString(string password)
        {
            var secureString = new SecureString();
            foreach (char c in password)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        private static void EnsureTestUserExistsAndSetInitialPassword(string domain, string adminUsername, SecureString adminPassword, string samAccountName, string initialPassword)
        {
            using var pc = new PrincipalContext(ContextType.Domain, domain, adminUsername, SecureStringToString(adminPassword));
            UserPrincipal? user = UserPrincipal.FindByIdentity(pc, IdentityType.SamAccountName, samAccountName);
            if (user == null)
            {
                user = new UserPrincipal(pc, samAccountName, initialPassword, true)
                {
                    DisplayName = samAccountName,
                    Enabled = true
                };
                user.Save();
            }
            else
            {
                user.SetPassword(initialPassword);
                user.ExpirePasswordNow(); // Force user to change password on next logon
                user.Enabled = true;
                user.Save();
            }
        }

        private static string? SecureStringToString(SecureString secureString)
        {
            if (secureString == null) return null;
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secureString);
                return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(valuePtr);
            }
            finally
            {
                if (valuePtr != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(valuePtr);
                }
            }
        }

        [TestMethod]
        public void Reset_ValidUser_ResetsPasswordAndReturnsTempPassword()
        {
            // Arrange
            var user = new User(_testDomain!, _testUserToResetSAM!, "Test User", "", "", "", "", "", null); // Only SAMAccountName and Domain are crucial for lookup

            // Act
            string? newTempPassword = _passwordResetService?.Reset(user);

            // Assert
            Assert.IsNotNull(newTempPassword);
            // Verify password format (Gyre@DD.MM.YYYY)
            StringAssert.Matches(newTempPassword, new System.Text.RegularExpressions.Regex(@"Gyre@\d{2}\.\d{2}\.\d{4}"));

            // Optionally: Try to log in with the new password or verify LastPasswordSet date changed.
            // This would require more AD interaction.
            // For now, rely on the service reporting success (non-null result).
            DateTime? lastPasswordSet = _passwordResetService?.GetLastPasswordSetDate(user);
            Assert.IsNotNull(lastPasswordSet);
            Assert.IsTrue(lastPasswordSet.Value.Date == DateTime.Today.Date);
        }

        [TestMethod]
        public void GetLastPasswordSetDate_ValidUser_ReturnsCorrectDate()
        {
            // Arrange
            var user = new User(_testDomain!, _testUserToResetSAM!, "Test User", "", "", "", "", "", null);

            // Act
            DateTime? lastPasswordSet = _passwordResetService?.GetLastPasswordSetDate(user);

            // Assert
            Assert.IsNotNull(lastPasswordSet);
            // The exact date depends on when the user's password was last set.
            // For a newly created/reset user in TestInitialize, it should be today.
            Assert.IsTrue(lastPasswordSet.Value.Date == DateTime.Today.Date);
        }

        [TestMethod]
        public void Reset_NonExistentUser_ReturnsNullAndLogsError()
        {
            // Arrange
            var nonExistentUser = new User(_testDomain!, "NonExistentSAMUser12345", "Non Existent", "", "", "", "", "", null);

            // Act
            string? result = _passwordResetService?.Reset(nonExistentUser);

            // Assert
            Assert.IsNull(result);
            // Verify that the logger was called with an error about the user not found.
            // This requires making ILoggingService a mockable interface and verifying its calls.
            // For integration tests, we just check for null and trust AD's response.
        }
    }
}