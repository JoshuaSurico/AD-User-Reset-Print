using System.IO;
using System.Text.Json;

namespace AD_User_Reset_Print.Services
{
    public class JsonManagerService : IJsonManagerService
    {
        private readonly ILoggingService _logger;

        // Constructor for dependency injection
        public JsonManagerService(ILoggingService logger) => _logger = logger;

        public void SaveToJson<T>(List<T> items, string filePath, bool clearExisting = false)
        {
            if (clearExisting)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        _logger.Log($"Cleared existing file: {filePath}", LogLevel.Debug);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error clearing existing file '{filePath}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            List<T> existingItems = ReadFromJson<T>(filePath);
            existingItems.AddRange(items);

            JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
            string jsonData = JsonSerializer.Serialize(existingItems, jsonSerializerOptions);

            try
            {
                File.WriteAllText(filePath, jsonData);
                _logger.Log($"Saved {items.Count} items to {filePath}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _logger.Log($"Error saving JSON to '{filePath}': {ex.Message}", LogLevel.Error);
            }
        }

        public List<T> ReadFromJson<T>(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.Log($"File not found: {filePath}. Returning empty list.", LogLevel.Debug);
                return [];
            }

            try
            {
                string jsonData = File.ReadAllText(filePath);

                if (!string.IsNullOrEmpty(jsonData))
                {
                    var result = JsonSerializer.Deserialize<List<T>>(jsonData);
                    _logger.Log($"Read {result?.Count ?? 0} items from {filePath}.", LogLevel.Info);
                    return result ?? []; // Ensure to return an empty list if deserialization results in null
                }
            }
            catch (JsonException ex)
            {
                _logger.Log($"Error deserializing JSON from '{filePath}': {ex.Message}", LogLevel.Error);
            }
            catch (Exception ex)
            {
                _logger.Log($"An unexpected error occurred while reading from '{filePath}': {ex.Message}", LogLevel.Error);
            }

            return [];
        }
    }
}