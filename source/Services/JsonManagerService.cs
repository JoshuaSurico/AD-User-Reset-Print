using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services
{
    internal static class JsonManagerService
    {
        public static void SaveToJson<T>(List<T> items, string filePath, bool clearExisting = false)
        {
            if (clearExisting)
            {
                // Delete the file if it exists
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            List<T> existingItems = ReadFromJson<T>(filePath);
            existingItems.AddRange(items);

            // Serialize the combined data back to JSON and save it to the file.
            JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
            var options = jsonSerializerOptions;
            string jsonData = JsonSerializer.Serialize(existingItems, options);
            File.WriteAllText(filePath, jsonData);
        }

        public static List<T> ReadFromJson<T>(string filePath)
        {
            // Return an empty list if the file doesn't exist.
            if (!File.Exists(filePath)) return new List<T>();

            try
            {
                string jsonData = File.ReadAllText(filePath);

                if (!string.IsNullOrEmpty(jsonData))
                {
                    return JsonSerializer.Deserialize<List<T>>(jsonData);
                }
            }
            catch (JsonException ex)
            {
                // Handle any exception that may occur during deserialization
                Console.WriteLine("Error deserializing JSON: " + ex.Message);
            }

            return new List<T>();
        }
    }
}
