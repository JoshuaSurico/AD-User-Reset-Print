using AD_User_Reset_Print.Services; // Your JsonManagerService, IJsonManagerService, ILoggingService
using AD_User_Reset_Print.Tests.TestHelpers; // Your TestLogger
using Microsoft.VisualStudio.TestTools.UnitTesting; // Correct using for MSTest
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AD_User_Reset_Print.Tests.Unit.Services
{
    [TestClass] // MSTest attribute for a test class
    public class JsonManagerServiceTests
    {
        private string? _testDirectory;
        private TestLogger? _testLogger;
        private JsonManagerService? _jsonManagerService;

        // MSTest attribute for setup method, runs before each test method
        [TestInitialize]
        public void TestInitialize()
        {
            // Create a unique temporary directory for each test run
            _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_testDirectory);

            _testLogger = new TestLogger();
            _jsonManagerService = new JsonManagerService(_testLogger);
        }

        // MSTest attribute for cleanup method, runs after each test method
        [TestCleanup]
        public void TestCleanup()
        {
            // Clean up the temporary directory after each test
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true); // Delete recursively
            }
        }

        private string GetTestFilePath(string fileName = "testdata.json")
        {
            return Path.Combine(_testDirectory!, fileName);
        }

        // --- SaveToJson Tests ---

        [TestMethod] // MSTest attribute for a test method
        public void SaveToJson_NewFile_SavesDataCorrectly()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var items = new List<string> { "item1", "item2" };

            // Act
            _jsonManagerService!.SaveToJson(items, filePath);

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(2, deserialized.Count);
            CollectionAssert.Contains(deserialized, "item1");
            CollectionAssert.Contains(deserialized, "item2");
            Assert.IsTrue(_testLogger!.ContainsLog("Saved 2 items to", LogLevel.Info));
        }

        [TestMethod]
        public void SaveToJson_ExistingFile_AppendsData()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var initialItems = new List<string> { "initial1" };
            _jsonManagerService!.SaveToJson(initialItems, filePath); // Save initial data

            var newItems = new List<string> { "new1", "new2" };

            // Act
            _jsonManagerService.SaveToJson(newItems, filePath); // Append new data

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(3, deserialized.Count); // initial1, new1, new2
            CollectionAssert.Contains(deserialized, "initial1");
            CollectionAssert.Contains(deserialized, "new1");
            CollectionAssert.Contains(deserialized, "new2");
        }

        [TestMethod]
        public void SaveToJson_ClearExistingTrue_OverwritesData()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var initialItems = new List<string> { "initial1", "initial2" };
            _jsonManagerService!.SaveToJson(initialItems, filePath); // Save initial data

            var newItems = new List<string> { "onlythis" };

            // Act
            _jsonManagerService.SaveToJson(newItems, filePath, true); // Overwrite with new data

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1, deserialized.Count); // Should only have one item now
            CollectionAssert.Contains(deserialized, "onlythis");
            Assert.IsTrue(_testLogger!.ContainsLog("Cleared existing file:", LogLevel.Debug));
        }

        [TestMethod]
        public void SaveToJson_EmptyList_SavesEmptyList_WhenClearingExisting()
        {
            // Arrange
            var filePath = GetTestFilePath();
            File.WriteAllText(filePath, JsonSerializer.Serialize(new List<string> { "oldData" })); // Ensure file exists with content
            var items = new List<string>();

            // Act
            _jsonManagerService!.SaveToJson(items, filePath, true); // Clear existing and save empty list

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(0, deserialized.Count);
        }

        [TestMethod]
        public void SaveToJson_EmptyList_AppendsToExisting_WhenNotClearingExisting()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var initialItems = new List<string> { "initialData" };
            _jsonManagerService!.SaveToJson(initialItems, filePath);

            var itemsToSave = new List<string>(); // Empty list

            // Act
            _jsonManagerService.SaveToJson(itemsToSave, filePath, false); // Append empty list

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1, deserialized.Count); // Should still contain initialData
            CollectionAssert.Contains(deserialized, "initialData");
        }


        // --- ReadFromJson Tests ---

        [TestMethod]
        public void ReadFromJson_NonExistentFile_ReturnsEmptyList()
        {
            // Arrange
            var filePath = GetTestFilePath("nonexistent.json");

            // Act
            var items = _jsonManagerService!.ReadFromJson<string>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
            Assert.IsTrue(_testLogger!.ContainsLog("File not found:", LogLevel.Debug));
        }

        [TestMethod]
        public void ReadFromJson_EmptyFile_ReturnsEmptyList()
        {
            // Arrange
            var filePath = GetTestFilePath("empty.json");
            File.WriteAllText(filePath, ""); // Create an empty file

            // Act
            var items = _jsonManagerService!.ReadFromJson<string>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);

            // Assert that NO error log was recorded for this specific scenario
            // (You might have other logs, but not an ERROR log)
            Assert.IsFalse(_testLogger!.Logs.Any(l => l.Level == LogLevel.Error));
            // Or, more specifically, ensure no deserialization errors were logged
            Assert.IsFalse(_testLogger.ContainsLog("Error deserializing JSON:", LogLevel.Error));

            // You could also assert that no logs at all were recorded if that's the expected behavior,
            // or only info/debug logs depending on other parts of your ReadFromJson logic.
            // In this case, with the current ReadFromJson, no logs are expected for an empty file.
            Assert.AreEqual(0, _testLogger.Logs.Count);
        }

        [TestMethod]
        public void ReadFromJson_ValidJson_ReturnsCorrectData()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var dataToSave = new List<int> { 100, 200, 300 };
            File.WriteAllText(filePath, JsonSerializer.Serialize(dataToSave));

            // Act
            var items = _jsonManagerService!.ReadFromJson<int>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(3, items.Count);
            CollectionAssert.Contains(items, 100);
            CollectionAssert.Contains(items, 200);
            CollectionAssert.Contains(items, 300);
        }

        [TestMethod]
        public void ReadFromJson_InvalidJson_ReturnsEmptyListAndLogsError()
        {
            // Arrange
            var filePath = GetTestFilePath("invalid.json");
            File.WriteAllText(filePath, "{ this is not valid json ["); // Write invalid JSON

            // Act
            var items = _jsonManagerService!.ReadFromJson<string>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
            Assert.IsTrue(_testLogger!.ContainsLog("Error deserializing JSON from", LogLevel.Error));
        }

        [TestMethod]
        public void ReadFromJson_FileWithValidButEmptyJsonArray_ReturnsEmptyList()
        {
            // Arrange
            var filePath = GetTestFilePath("emptyarray.json");
            File.WriteAllText(filePath, "[]"); // Valid empty JSON array

            // Act
            var items = _jsonManagerService!.ReadFromJson<string>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
            Assert.IsTrue(_testLogger!.ContainsLog("Read 0 items from", LogLevel.Info));
        }

        [TestMethod]
        public void ReadFromJson_FileWithValidButEmptyJsonObject_ReturnsEmptyListAndLogsError()
        {
            // Arrange
            var filePath = GetTestFilePath("emptyobject.json");
            File.WriteAllText(filePath, "{}"); // Valid empty JSON object (not a list<T>)

            // Act
            var items = _jsonManagerService!.ReadFromJson<string>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
            // Deserialization to List<T> will fail from an object, so expect an error log
            Assert.IsTrue(_testLogger!.ContainsLog("Error deserializing JSON from", LogLevel.Error));
        }

        // You could add tests for different types (e.g., custom objects) if your model supports it
        public class TestObject
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public override bool Equals(object obj)
            {
                return obj is TestObject other && Id == other.Id && Name == other.Name;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Id, Name);
            }
        }

        [TestMethod]
        public void ReadFromJson_ValidCustomObjects_ReturnsCorrectData()
        {
            // Arrange
            var filePath = GetTestFilePath("customobjects.json");
            var dataToSave = new List<TestObject>
            {
                new TestObject { Id = 1, Name = "Alpha" },
                new TestObject { Id = 2, Name = "Beta" }
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(dataToSave));

            // Act
            var items = _jsonManagerService!.ReadFromJson<TestObject>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);
            CollectionAssert.Contains(items, new TestObject { Id = 1, Name = "Alpha" });
            CollectionAssert.Contains(items, new TestObject { Id = 2, Name = "Beta" });
        }

        [TestMethod]
        public void SaveToJson_And_ReadFromJson_Roundtrip_Works()
        {
            // Arrange
            var filePath = GetTestFilePath("roundtrip.json");
            var originalItems = new List<TestObject>
            {
                new() { Id = 10, Name = "Round" },
                new() { Id = 20, Name = "Trip" }
            };

            // Act
            _jsonManagerService!.SaveToJson(originalItems, filePath);
            var retrievedItems = _jsonManagerService.ReadFromJson<TestObject>(filePath);

            // Assert
            Assert.IsNotNull(retrievedItems);
            Assert.AreEqual(originalItems.Count, retrievedItems.Count);
            // Use CollectionAssert.AreEquivalent for comparing lists of custom objects
            CollectionAssert.AreEquivalent(originalItems, retrievedItems);
        }
    }
}