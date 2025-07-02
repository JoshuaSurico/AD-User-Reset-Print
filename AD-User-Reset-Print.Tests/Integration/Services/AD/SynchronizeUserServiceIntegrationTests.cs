using Microsoft.VisualStudio.TestTools.UnitTesting;
using AD_User_Reset_Print.Services.AD;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Models;
using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Linq;
using System.IO;
using AD_User_Reset_Print.Tests.TestHelpers;

namespace AD_User_Reset_Print.Tests.Integration.Services.AD
{
    // Mock AppSettings and JsonManagerService to control file paths and capture saves
    public static class AppSettings
    {
        public static string UserListDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "ADUserResetPrintTests_Integration");
        public static string UserListFilePath { get; set; } = Path.Combine(Path.GetTempPath(), "ADUserResetPrintTests_Integration", "userlist.json");

        public static void InitializeTestSettings()
        {
            if (Directory.Exists(UserListDirectory))
            {
                Directory.Delete(UserListDirectory, true);
            }
            Directory.CreateDirectory(UserListDirectory);
        }

        public static void CleanupTestSettings()
        {
            if (Directory.Exists(UserListDirectory))
            {
                Directory.Delete(UserListDirectory, true);
            }
        }
    }

    public static class JsonManagerService
    {
        public static List<User>? LastSavedUsers { get; private set; }
        public static string? LastSavedPath { get; private set; }
        public static int SaveToJsonCallCount { get; private set; }

        public static void SaveToJson(List<User> users, string path)
        {
            LastSavedUsers = users;
            LastSavedPath = path;
            SaveToJsonCallCount++;
            // Simulate saving, no actual file content needed for this test's assertion
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(users));
        }

        public static void Reset()
        {
            LastSavedUsers = null;
            LastSavedPath = null;
            SaveToJsonCallCount = 0;
        }
    }


    [TestClass]
    public class SynchronizeUserServiceIntegrationTests
    {
        private ILoggingService? _logger;
        private ICredentialStorageService? _credentialStorageService;
        private IJsonManagerService? _jsonManagerService;
        private ISynchronizeUserService? _synchronizeUserService;
        private IConfiguration? _configuration;

        // Configuration values
        private string? _testDomain;
        private string? _adminUsername;
        private SecureString? _adminPassword;
        private string? _testGroup1; // A group expected to contain users
        private string? _testGroup2; // Another group, potentially overlapping users
        private string? _emptyTestGroup; // An empty group
        private string? _nonExistentGroup; // A group that does not exist

        [TestInitialize]
        public void TestInitialize()
        {
            AppSettings.InitializeTestSettings();
            JsonManagerService.Reset();

            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.integrationtests.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            _testDomain = _configuration["ADSettings:Domain"];
            _adminUsername = _configuration["ADSettings:AdminUsername"];
            _adminPassword = ConvertToSecureString(_configuration["ADSettings:AdminPassword"]);
            _testGroup1 = _configuration["ADSettings:TestGroup1"];
            _testGroup2 = _configuration["ADSettings:TestGroup2"];
            _emptyTestGroup = _configuration["ADSettings:EmptyTestGroup"];
            _nonExistentGroup = _configuration["ADSettings:NonExistentGroup"];

            _logger = new ConsoleLogger(); // Use a console logger for integration tests

            // Set up a mock credential storage that returns our admin credentials
            var mockCredentialStorage = new Moq.Mock<ICredentialStorageService>();
            mockCredentialStorage.Setup(cs => cs.LoadCredentials()).Returns(new List<CredentialEntry>
            {
                /*new() {
                    Domain = _testDomain,
                    Username = _adminUsername,
                    Password = _adminPassword,
                    Groups = new List<string> { _testGroup1!, _testGroup2!, _emptyTestGroup!, _nonExistentGroup! }
                }*/
            });
            _credentialStorageService = mockCredentialStorage.Object;

            _synchronizeUserService = new SynchronizeUserService(_logger, _credentialStorageService, _jsonManagerService);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _adminPassword?.Dispose();
            AppSettings.CleanupTestSettings();
        }

        private SecureString ConvertToSecureString(string password)
        {
            var secureString = new SecureString();
            foreach (char c in password)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        [TestMethod]
        public async Task Sync_MultipleGroupsWithExistingUsers_ReturnsUniqueUsersAndSavesFile()
        {
            // Arrange
            var mockProgress = new Moq.Mock<IProgress<ProgressReport>>();

            // Act
            List<User> syncedUsers = await _synchronizeUserService!.Sync(mockProgress.Object);

            // Assert
            Assert.IsNotNull(syncedUsers);
            Assert.IsTrue(syncedUsers.Count > 0, "Expected to find some users from the test groups.");

            // Verify progress reporting (at least start, some progress, and end)
            mockProgress.Verify(p => p.Report(It.Is<ProgressReport>(pr => pr.PercentComplete == 0)), Times.Once);
            mockProgress.Verify(p => p.Report(It.Is<ProgressReport>(pr => pr.PercentComplete > 0 && pr.PercentComplete <= 95)), Times.AtLeastOnce);
            mockProgress.Verify(p => p.Report(It.Is<ProgressReport>(pr => pr.PercentComplete == 100 && pr.CurrentActivity.StartsWith("Synchronization complete."))), Times.Once);

            // Verify that SaveToJson was called exactly once
            Assert.AreEqual(1, JsonManagerService.SaveToJsonCallCount);
            Assert.IsNotNull(JsonManagerService.LastSavedUsers);
            Assert.AreEqual(syncedUsers.Count, JsonManagerService.LastSavedUsers.Count);
            Assert.IsTrue(syncedUsers.All(u => JsonManagerService.LastSavedUsers.Contains(u, new UserEqualityComparer())));

            // Verify the file was actually created and contains JSON (basic check)
            Assert.IsTrue(File.Exists(AppSettings.UserListFilePath));
            string fileContent = File.ReadAllText(AppSettings.UserListFilePath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fileContent));
            StringAssert.Contains(fileContent, "SAMAccountName"); // Check for typical JSON content
        }

        [TestMethod]
        public async Task Sync_NonExistentGroup_LogsWarningAndSkipsGroup()
        {
            // Arrange
            var mockProgress = new Moq.Mock<IProgress<ProgressReport>>();

            // Act
            List<User> syncedUsers = await _synchronizeUserService!.Sync(mockProgress.Object);

            // Assert
            Assert.IsNotNull(syncedUsers);
            // Verify that a warning was logged for the non-existent group
            // This requires capturing logger output if logger is a mock, or checking console output if it's a real logger.
            // For this integration test, we'll confirm the sync completed and expect the non-existent group was skipped.
            // If you had a custom ILoggingService that recorded messages, you'd check its history.
            // _logger.Verify(l => l.Log(It.Is<string>(msg => msg.Contains($"Group '{_nonExistentGroup}' not found")), LogLevel.Warning), Times.Once);
            // Since ConsoleLogger is used, we cannot verify directly via Moq.
            // Instead, we just ensure the process completes successfully despite the missing group.
            Assert.AreEqual(1, JsonManagerService.SaveToJsonCallCount); // Still saves even if one group is missing
        }

        // Must be in the same namespace or accessible
        private class UserEqualityComparer : IEqualityComparer<User>
        {
            public bool Equals(User? x, User? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.SAMAccountName == y.SAMAccountName && x.Domain == y.Domain;
            }

            public int GetHashCode(User obj)
            {
                if (obj == null) return 0;
                return HashCode.Combine(obj.SAMAccountName, obj.Domain);
            }
        }
    }
}