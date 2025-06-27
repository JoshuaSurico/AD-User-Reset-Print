using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AD_User_Reset_Print.Services
{
    public static class CredentialStorageService // Made static for easy access without instantiation
    {
        // --- PUBLIC METHODS ---

        /// <summary>
        /// Loads and decrypts the list of CredentialEntry objects from user settings.
        /// </summary>
        /// <returns>A list of CredentialEntry objects, or an empty list if none are found or an error occurs.</returns>
        public static List<CredentialEntry> LoadCredentials()
        {
            string protectedJson = Credentials.Default.AllCredentials;
            if (string.IsNullOrEmpty(protectedJson))
            {
                return []; // No credentials saved yet
            }
            else
            {
                try
                {
                    // 1. Unprotect the entire JSON string
                    string json = Unprotect(protectedJson);
                    // 2. Deserialize the JSON string into a list of CredentialEntry
                    List<CredentialEntry> loadedCredentials = JsonConvert.DeserializeObject<List<CredentialEntry>>(json);
                    return loadedCredentials ?? [];
                }
                catch (JsonException ex)
                {
                    MessageBox.Show($"Error deserializing credentials: {ex.Message}\nSaved data might be corrupted.", "Credential Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return [];
                }
                catch (CryptographicException ex)
                {
                    MessageBox.Show($"Error decrypting credentials: {ex.Message}\nSaved data might be corrupted or protected for a different user/machine.", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return [];
                }
                catch (FormatException ex)
                {
                    MessageBox.Show($"Error: Invalid format for encrypted data. {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return [];
                }
                catch (Exception ex) // Catch any other unexpected errors
                {
                    MessageBox.Show($"An unexpected error occurred loading credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return [];
                }
            }
        }

        /// <summary>
        /// Serializes and encrypts the entire list of CredentialEntry objects and saves them to user settings.
        /// </summary>
        /// <param name="credentialsToSave">The list of CredentialEntry objects to save.</param>
        public static void SaveCredentials(List<CredentialEntry> credentialsToSave)
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
            }
            catch (JsonException ex)
            {
                MessageBox.Show($"Error serializing credentials: {ex.Message}", "Credential Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (CryptographicException ex)
            {
                MessageBox.Show($"Error encrypting credentials: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) // Catch any other unexpected errors
            {
                MessageBox.Show($"An unexpected error occurred saving credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Deletes all saved credentials from the application settings.
        /// </summary>
        public static void ClearAllCredentials()
        {
            try
            {
                Credentials.Default.AllCredentials = string.Empty; // Clear the setting
                Credentials.Default.Save(); // Persist the change
                MessageBox.Show("All saved credentials have been deleted.", "Credentials Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Helper method to check if any credentials exist without performing a full load.
        /// </summary>
        public static bool AreCredentialsSaved()
        {
            return !string.IsNullOrEmpty(Credentials.Default.AllCredentials);
        }

        // --- PRIVATE HELPER METHODS FOR PROTECTION ---

        /// <summary>
        /// Encrypts a plain text string using DPAPI.
        /// </summary>
        /// <param name="plainText">The string to encrypt.</param>
        /// <returns>The Base64 encoded protected string.</returns>
        private static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] userData = Encoding.UTF8.GetBytes(plainText);
            // DataProtectionScope.CurrentUser means only the current user on the current machine can unprotect.
            // DataProtectionScope.LocalMachine means any user on the current machine can unprotect (less secure).
            byte[] protectedData = ProtectedData.Protect(userData, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedData);
        }

        /// <summary>
        /// Decrypts a protected string using DPAPI.
        /// </summary>
        /// <param name="encryptedText">The Base64 encoded protected string.</param>
        /// <returns>The decrypted plain text string.</returns>
        private static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            byte[] protectedData = Convert.FromBase64String(encryptedText);
            byte[] userData = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(userData);
        }
    }
}