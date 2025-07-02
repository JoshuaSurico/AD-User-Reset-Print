using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Services.AD;
using AD_User_Reset_Print.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Documents;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace AD_User_Reset_Print.Tests.Integration.Services.AD
{
    [TestClass]
    public class ADSourceCheckServiceIntegrationTests
    {
        private ILoggingService? _logger;
        private ADSourceCheckService? _adSourceCheckService;
        private IConfiguration? _configuration;

        // Configuration values from appsettings.integrationtests.json
        private string? _testDomain;
        private string? _adminUsername;
        private string? _standardUsername;
        private string? _testGroup1; // A group with members the test admin user has reset permission for
        private string? _emptyTestGroup; // A group that exists but has no members
        private string? _nonExistentGroup; // A group that does not exist in AD

        // Config value from user secret
        private SecureString? _adminPassword;
        private SecureString? _standardPassword;

        [TestInitialize]
        public void TestInitialize()
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.integrationtests.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
                .Build();

            _testDomain = _configuration["ADSettings:Domain"] ?? throw new InvalidOperationException("ADSettings:Domain not configured.");
            _adminUsername = _configuration["ADSettings:AdminUsername"] ?? throw new InvalidOperationException("ADSettings:AdminUsername not configured.");
            _standardUsername = _configuration["ADSettings:StandardUsername"] ?? throw new InvalidOperationException("ADSettings:StandardUsername not configured.");
            _testGroup1 = _configuration["ADSettings:TestGroup1"] ?? throw new InvalidOperationException("ADSettings:TestGroup1 not configured.");
            _emptyTestGroup = _configuration["ADSettings:EmptyTestGroup"] ?? throw new InvalidOperationException("ADSettings:EmptyTestGroup not configured.");
            _nonExistentGroup = _configuration["ADSettings:NonExistentGroup"] ?? throw new InvalidOperationException("ADSettings:NonExistentGroup not configured.");

            string? adminPasswordFromSecrets = _configuration["ADSettings:AdminPassword"];
            if (string.IsNullOrEmpty(adminPasswordFromSecrets))
            {
                throw new InvalidOperationException(
                    "ADSettings:AdminPassword not found. " +
                    "Please set it using 'dotnet user-secrets set \"ADSettings:AdminPassword\" \"YourPassword\"' " +
                    "or as an environment variable."
                );
            }
            _adminPassword = ConvertToSecureString(adminPasswordFromSecrets);

            string? standardPasswordFromSecrets = _configuration["ADSettings:StandardPassword"];
            if (string.IsNullOrEmpty(standardPasswordFromSecrets))
            {
                throw new InvalidOperationException(
                    "ADSettings:StandardPassword not found. " +
                    "Please set it using 'dotnet user-secrets set \"ADSettings:StandardPassword\" \"YourPassword\"' " +
                    "or as an environment variable."
                );
            }
            _standardPassword = ConvertToSecureString(standardPasswordFromSecrets);

            _logger = new ConsoleLogger();
            _adSourceCheckService = new ADSourceCheckService(_logger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _adminPassword?.Dispose();
            _standardPassword?.Dispose();
        }

        private static SecureString ConvertToSecureString(string password)
        {
            ArgumentNullException.ThrowIfNull(password);

            var secureString = new SecureString();
            foreach (char c in password)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_ValidAdminUserAndDomain_ReturnsSuccessAndIsHighlyPrivileged()
        {
            // Arrange
            List<string> targetGroups = [_testGroup1]; // Use a configured group

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(_testDomain!, _adminUsername!, _adminPassword!, targetGroups);

            // Assert
            Assert.IsTrue(result.IsSuccessful, $"Expected IsSuccessful to be true. Error: {result.ErrorMessage}");
            Assert.IsTrue(result.IsHighlyPrivileged, "Expected IsHighlyPrivileged to be true for an admin user.");
            Assert.IsTrue(result.HasFullPermission, "Expected HasFullPermission to be true for an admin user.");
            Assert.IsTrue(result.TargetGroupPermissions.ContainsKey(_testGroup1), $"Expected TargetGroupPermissions to contain '{_testGroup1}'.");
            Assert.IsTrue(result.TargetGroupPermissions[_testGroup1], $"Expected admin user to have permission for '{_testGroup1}'.");
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage), $"Expected no error message but got: {result.ErrorMessage}");
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_StandardUserNoPermission_ReturnsFalseForTargetGroup()
        {
            // Arrange
            List<string> targetGroups = [_testGroup1];

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(_testDomain!, _standardUsername!, _standardPassword!, targetGroups);

            // Assert
            Assert.IsTrue(result.IsSuccessful, $"Expected IsSuccessful to be true for a valid user. Error: {result.ErrorMessage}");
            Assert.IsFalse(result.IsHighlyPrivileged, "Expected IsHighlyPrivileged to be false for a standard user.");
            Assert.IsFalse(result.HasFullPermission, "Expected HasFullPermission to be false for a standard user.");

            Assert.IsTrue(result.TargetGroupPermissions.ContainsKey(_testGroup1), $"Expected TargetGroupPermissions to contain '{_testGroup1}'.");
            Assert.IsFalse(result.TargetGroupPermissions[_testGroup1], $"Expected standard user to NOT have permission for '{_testGroup1}'.");
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_NonExistentDomain_ReturnsFailure()
        {
            // Arrange
            string nonExistentDomain = "nonexistent.example.com";
            List<string> targetGroups = ["AnyGroup"];

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(nonExistentDomain, _adminUsername!, _adminPassword!, targetGroups);

            // Assert
            Assert.IsFalse(result.IsSuccessful, "Expected IsSuccessful to be false for a non-existent domain.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage), "Expected an error message for a non-existent domain.");

            StringAssert.Contains(result.ErrorMessage!, $"Error: The AD server for '{nonExistentDomain}' is not operational or firewalled.", "Expected error message about AD server not operational, including the domain.");
            StringAssert.Contains(result.ErrorMessage!, nonExistentDomain, $"Expected the error message to mention the domain '{nonExistentDomain}'.");
            StringAssert.Contains(result.ErrorMessage!, "0x8007203A", "Expected the specific error code for server not operational.");
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_InvalidCredentials_ReturnsFailure()
        {
            // Arrange
            SecureString invalidPassword = ConvertToSecureString("WrongPassword!");
            List<string> targetGroups = [_testGroup1];

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(_testDomain!, _adminUsername!, invalidPassword, targetGroups);

            // Assert
            Assert.IsFalse(result.IsSuccessful, "Expected IsSuccessful to be false for invalid credentials.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage), "Expected an error message for invalid credentials.");
            StringAssert.Contains(result.ErrorMessage!, "Error: Authentication failed", "Expected authentication failed error message.");

            invalidPassword.Dispose();
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_NonExistentAdminUser_ReturnsFailure()
        {
            // Arrange
            string nonExistentUser = "nonexistentuser123";
            List<string> targetGroups = [_testGroup1];

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(_testDomain!, nonExistentUser, _adminPassword!, targetGroups);

            // Assert
            Assert.IsFalse(result.IsSuccessful, "Expected IsSuccessful to be false for a non-existent admin user.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage), "Expected an error message for a non-existent admin user.");
            StringAssert.Contains(result.ErrorMessage!, "Error: Authentication failed for user", "Expected 'Authentication failed' error message when user does not exist.");
            StringAssert.Contains(result.ErrorMessage!, nonExistentUser, "Expected error message to contain the non-existent username.");
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_MultipleTargetGroups_ReportsCorrectPermissions()
        {
            // Arrange
            List<string> targetGroups = [_testGroup1, _nonExistentGroup];

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(_testDomain!, _adminUsername!, _adminPassword!, targetGroups);

            // Assert
            // Assert overall success (after fixing the IsSuccessful logic in service if needed)
            Assert.IsTrue(result.IsSuccessful, $"Expected IsSuccessful to be true. Error: {result.ErrorMessage}");
            Assert.IsTrue(result.IsHighlyPrivileged, "Expected IsHighlyPrivileged to be true for admin.");
            Assert.IsTrue(result.HasFullPermission, "Expected HasFullPermission to be true for admin.");

            // Verify permissions for individual groups
            Assert.IsTrue(result.TargetGroupPermissions.ContainsKey(_testGroup1), "Expected _testGroup1 in permissions.");
            Assert.IsTrue(result.TargetGroupPermissions[_testGroup1], $"Expected admin to have permission for '{_testGroup1}'.");

            // For non-existent group:
            Assert.IsTrue(result.TargetGroupPermissions.ContainsKey(_nonExistentGroup), "Expected nonExistentGroup in permissions.");
            Assert.IsFalse(result.TargetGroupPermissions[_nonExistentGroup], $"Expected admin to NOT have permission for '{_nonExistentGroup}' (group not found).");
            StringAssert.Contains(result.ErrorMessage!, $"Target group '{_nonExistentGroup}' not found.", "Expected error message about non-existent group.");

            // Final check on overall ErrorMessage content (ensure only expected errors/warnings are there)
            StringAssert.Contains(result.ErrorMessage!, $"Target group '{_nonExistentGroup}' not found.");
        }

        [TestMethod]
        public async Task RunPermissionCheckAsync_TargetGroupCaseInsensitive_ReturnsCorrectPermissions()
        {
            // Arrange
            string testGroup1Lower = _testGroup1!.ToLowerInvariant();
            List<string> targetGroups = [testGroup1Lower]; // Use lowercase version of an existing group

            // Act
            PermissionCheckResult result = await _adSourceCheckService!.RunPermissionCheckAsync(_testDomain!, _adminUsername!, _adminPassword!, targetGroups);

            // Assert
            Assert.IsTrue(result.IsSuccessful, $"Expected IsSuccessful to be true. Error: {result.ErrorMessage}");
            Assert.IsTrue(result.TargetGroupPermissions.ContainsKey(testGroup1Lower), $"Expected TargetGroupPermissions to contain '{testGroup1Lower}'.");
            Assert.IsTrue(result.TargetGroupPermissions[testGroup1Lower], $"Expected admin user to have permission for '{testGroup1Lower}'.");
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage), $"Expected no error message but got: {result.ErrorMessage}");
        }
    }
}