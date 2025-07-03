using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Tests.TestHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AD_User_Reset_Print.Tests.Unit.Services
{
    [TestClass]
    public class JsonManagerServiceTests
    {
        private string? _testDirectory;
        private TestLogger? _testLogger;
        private JsonManagerService? _jsonManagerService;

        [TestInitialize]
        public void TestInitialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_testDirectory);
            _testLogger = new TestLogger();
            _jsonManagerService = new JsonManagerService(_testLogger);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        private string GetTestFilePath(string fileName = "testdata.json")
        {
            return Path.Combine(_testDirectory!, fileName);
        }

        // --- SaveToJson Tests ---

        [TestMethod]
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
            Assert.IsTrue(_testLogger!.ContainsLog("(Mode: Append)", LogLevel.Info));
        }

        [TestMethod]
        public void SaveToJson_ExistingFileWithOverwriteFalse_AppendsData()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var initialItems = new List<string> { "initial1" };
            // Save initial data, explicitly setting overwrite to true to start clean.
            _jsonManagerService!.SaveToJson(initialItems, filePath, overwrite: true);

            var newItems = new List<string> { "new1", "new2" };

            // Act
            // Append new data by calling with default (or explicit false) overwrite.
            _jsonManagerService.SaveToJson(newItems, filePath, overwrite: false);

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
        public void SaveToJson_OverwriteTrue_OverwritesData()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var initialItems = new List<string> { "initial1", "initial2" };
            _jsonManagerService!.SaveToJson(initialItems, filePath);

            var newItems = new List<string> { "onlythis" };

            // Act
            _jsonManagerService.SaveToJson(newItems, filePath, overwrite: true);

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1, deserialized.Count);
            CollectionAssert.Contains(deserialized, "onlythis");
            Assert.IsTrue(_testLogger!.ContainsLog("(Mode: Overwrite)", LogLevel.Info));
            Assert.IsFalse(_testLogger!.ContainsLog("Cleared existing file:", LogLevel.Debug));
        }

        [TestMethod]
        public void SaveToJson_EmptyList_SavesEmptyList_WhenOverwriting()
        {
            // Arrange
            var filePath = GetTestFilePath();
            File.WriteAllText(filePath, JsonSerializer.Serialize(new List<string> { "oldData" }));
            var items = new List<string>();

            // Act
            _jsonManagerService!.SaveToJson(items, filePath, overwrite: true);

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(0, deserialized.Count);
        }

        [TestMethod]
        public void SaveToJson_EmptyList_AppendsToExisting_WhenNotOverwriting()
        {
            // Arrange
            var filePath = GetTestFilePath();
            var initialItems = new List<string> { "initialData" };
            _jsonManagerService!.SaveToJson(initialItems, filePath, overwrite: true);

            var itemsToSave = new List<string>();

            // Act
            _jsonManagerService.SaveToJson(itemsToSave, filePath, overwrite: false);

            // Assert
            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(content);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(1, deserialized.Count);
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
            File.WriteAllText(filePath, "");

            // Act
            var items = _jsonManagerService!.ReadFromJson<string>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(0, items.Count);
            Assert.IsTrue(_testLogger!.ContainsLog("File", LogLevel.Debug));
            Assert.IsTrue(_testLogger!.ContainsLog("is empty. Returning empty list.", LogLevel.Debug));
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

        public class TestObject
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public override bool Equals(object? obj) => obj is TestObject other && Id == other.Id && Name == other.Name;
            public override int GetHashCode() => HashCode.Combine(Id, Name);
        }

        [TestMethod]
        public void ReadFromJson_ValidCustomObjects_ReturnsCorrectData()
        {
            // Arrange
            var filePath = GetTestFilePath("customobjects.json");
            var dataToSave = new List<TestObject>
            {
                new() { Id = 1, Name = "Alpha" },
                new() { Id = 2, Name = "Beta" }
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(dataToSave));

            // Act
            var items = _jsonManagerService!.ReadFromJson<TestObject>(filePath);

            // Assert
            Assert.IsNotNull(items);
            Assert.AreEqual(2, items.Count);
            CollectionAssert.AreEquivalent(dataToSave, items);
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
            _jsonManagerService!.SaveToJson(originalItems, filePath, overwrite: true);
            var retrievedItems = _jsonManagerService.ReadFromJson<TestObject>(filePath);

            // Assert
            Assert.IsNotNull(retrievedItems);
            CollectionAssert.AreEquivalent(originalItems, retrievedItems);
        }
    }
}