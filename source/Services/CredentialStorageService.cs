using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Properties;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace AD_User_Reset_Print.Services
{
    public class CredentialStorageService(ILoggingService logger) : ICredentialStorageService
    {
        public List<CredentialEntry> LoadCredentials()
        {
            string protectedJson = Credentials.Default.AllCredentials;
            if (string.IsNullOrEmpty(protectedJson))
            {
                logger.Log("No credentials saved yet, returning empty list.", LogLevel.Info);
                return [];
            }

            try
            {
                // 1. Unprotect the entire JSON string
                string json = Unprotect(protectedJson);

                // 2. Deserialize the JSON string into a list of CredentialEntry
                // The CredentialEntry will now handle its own SecureString hydration from EncryptedPasswordBytesForStorage.
                List<CredentialEntry>? loadedCredentials = JsonConvert.DeserializeObject<List<CredentialEntry>>(json);

                logger.Log($"Loaded {loadedCredentials?.Count ?? 0} credentials.", LogLevel.Info);
                return loadedCredentials ?? [];
            }
            catch (Exception ex) when (ex is JsonException || ex is CryptographicException || ex is FormatException)
            {
                logger.Log($"Failed to load or decrypt credentials. Data may be corrupt. Details: {ex.Message}", LogLevel.Error);
                return [];
            }
            catch (Exception ex)
            {
                logger.Log($"An unexpected error occurred while loading credentials. Details: {ex.Message}", LogLevel.Error);
                return [];
            }
        }

        public void SaveCredentials(List<CredentialEntry> credentialsToSave)
        {
            try
            {
                // 1. Serialize the list of CredentialEntry objects into a JSON string
                string json = JsonConvert.SerializeObject(credentialsToSave);

                // 2. Protect the entire JSON string
                string protectedJson = Protect(json);

                // 3. Save the protected JSON string to settings
                Credentials.Default.AllCredentials = protectedJson;
                Credentials.Default.Save();
                logger.Log($"Saved {credentialsToSave?.Count ?? 0} credentials.", LogLevel.Info);
            }
            catch (Exception ex) when (ex is JsonException || ex is CryptographicException)
            {
                logger.Log($"Failed to save or encrypt credentials. Details: {ex.Message}", LogLevel.Error);
            }
            catch (Exception ex)
            {
                logger.Log($"An unexpected error occurred while saving credentials. Details: {ex.Message}", LogLevel.Error);
            }
        }

        public void ClearAllCredentials()
        {
            try
            {
                Credentials.Default.AllCredentials = string.Empty;
                Credentials.Default.Save();
                logger.Log("All saved credentials have been cleared.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                logger.Log($"Failed to clear credentials. Details: {ex.Message}", LogLevel.Error);
            }
        }

        public bool AreCredentialsSaved()
        {
            // Call LoadCredentials to get the actual list of credentials.
            // This handles decryption and deserialization.
            List<CredentialEntry> loaded = LoadCredentials();

            // Check if the loaded list contains any items.
            bool hasCredentials = loaded.Count != 0;

            logger.Log($"AreCredentialsSaved() check: {loaded.Count} credentials found. Result: {hasCredentials}", LogLevel.Debug);
            return hasCredentials;
        }

        private static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] userData = Encoding.UTF8.GetBytes(plainText);
            byte[] protectedData = ProtectedData.Protect(userData, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedData);
        }

        private static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            byte[] protectedData = Convert.FromBase64String(encryptedText);
            byte[] userData = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(userData);
        }
    }
}