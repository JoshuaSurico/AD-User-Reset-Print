using AD_User_Reset_Print.Services.AD;

namespace AD_User_Reset_Print.Tests.Services
{
    [TestClass]
    public class PasswordResetServiceTests
    {
        [TestMethod]
        public void GenerateTempPasswordForDate_WithSpecificDate_ReturnsCorrectlyFormattedString()
        {
            // Arrange: Define a specific date to test.
            var testDate = new DateTime(2024, 10, 27);
            var expectedPassword = "Gyre@27.10.2024";

            // Act: Call the method with the test date.
            string actualPassword = PasswordResetService.GenerateTempPasswordForDate(testDate);

            // Assert: Verify that the output matches the expected format.
            Assert.AreEqual(expectedPassword, actualPassword, "The generated password format is incorrect.");
        }

        // --- Note on Testing Other Methods ---
        // The `Reset` and `GetLastPasswordSetDate` methods directly call Active Directory.
        // A true *unit test* for these would require significant refactoring of PasswordResetService
        // to "inject" a fake or "mocked" Active Directory service.
        // Testing them as-is would be an *integration test*, which is also valuable but
        // requires a live test AD environment.
    }
}