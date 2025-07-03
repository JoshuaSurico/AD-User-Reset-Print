// File: Services/JsonManagerService.cs
using System.IO;
using System.Text.Json;

namespace AD_User_Reset_Print.Services
{
    public class JsonManagerService : IJsonManagerService
    {
        private readonly ILoggingService _logger;

        public JsonManagerService(ILoggingService logger) => _logger = logger;

        // MODIFIED: Renamed 'clearExisting' to 'overwrite' for clarity and simplified logic.
        public void SaveToJson<T>(List<T> items, string filePath, bool overwrite = false)
        {
            List<T> itemsToSave;

            // If we are NOT overwriting, we read the existing items and append the new ones.
            if (!overwrite)
            {
                itemsToSave = ReadFromJson<T>(filePath);
                itemsToSave.AddRange(items);
            }
            else
            {
                // If we ARE overwriting, the list to save is just the new list.
                itemsToSave = items;
            }

            JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
            string jsonData = JsonSerializer.Serialize(itemsToSave, jsonSerializerOptions);

            try
            {
                // File.WriteAllText overwrites the file if it exists, or creates it if it doesn't.
                File.WriteAllText(filePath, jsonData);
                _logger.Log($"Saved {itemsToSave.Count} items to {filePath}. (Mode: {(overwrite ? "Overwrite" : "Append")})", LogLevel.Info);
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
                // This is not an error, just a state. Log as debug.
                _logger.Log($"File not found: {filePath}. Returning empty list.", LogLevel.Debug);
                return [];
            }

            try
            {
                string jsonData = File.ReadAllText(filePath);

                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    _logger.Log($"File '{filePath}' is empty. Returning empty list.", LogLevel.Debug);
                    return [];
                }

                var result = JsonSerializer.Deserialize<List<T>>(jsonData);
                _logger.Log($"Read {result?.Count ?? 0} items from {filePath}.", LogLevel.Info);
                return result ?? []; // Return an empty list if deserialization results in null

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