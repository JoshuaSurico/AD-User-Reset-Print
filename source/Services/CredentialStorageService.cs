using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services
{
    public class CredentialStorageService
    {
        public static List<CredentialEntry> LoadCredentials()
        {
            string json = Credentials.Default.AllCredentials;
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }
            else
            {
                try
                {
                    List<CredentialEntry> loadedCredentials = JsonConvert.DeserializeObject<List<CredentialEntry>>(json);
                    return loadedCredentials ?? [];
                }
                catch (JsonException ex)
                {
                    // Log the error or handle it as appropriate (e.g., return empty list)
                    System.Windows.MessageBox.Show($"Error loading credentials from settings: {ex.Message}", "Credential Load Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return [];
                }
            }
        }

        // This method will take the UNPROTECTED password, protect it, and then save the list.
        public static void SaveCredential(CredentialEntry credentialToSave)
        {
            List<CredentialEntry> currentCredentials = LoadCredentials(); // Get current list

            // Protect the password before saving
            credentialToSave.Password = Protect(credentialToSave.Password);

            // For simplicity, we'll replace or add the first credential.
            // If you need to manage multiple, more complex logic for index/ID lookup is needed.
            if (currentCredentials.Count != 0)
            {
                currentCredentials[0] = credentialToSave; // Update the first (or only) entry
            }
            else
            {
                currentCredentials.Add(credentialToSave); // Add as new
            }
            string json = JsonConvert.SerializeObject(currentCredentials);
            Credentials.Default.AllCredentials = json;

            Credentials.Default.Save(); // Always save changes to the settings file
        }

        // Methods for encryption/decryption (kept private as they are internal to this service's logic)
        private static string Protect(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            byte[] userData = Encoding.UTF8.GetBytes(text);
            byte[] protectedData = ProtectedData.Protect(userData, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedData);
        }

        // This method is public because it might be needed by consuming classes (e.g., App.xaml.cs)
        // if they need to directly unprotect a password from a loaded CredentialEntry.
        // However, in this specific flow, App.xaml.cs only checks for *existence*,
        // and Login.xaml.cs gets the password from the user. So for now, keep it public,
        // but it might not be explicitly called if the flow doesn't require pre-filling passwords.
        public static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            try
            {
                byte[] protectedData = Convert.FromBase64String(encryptedText);
                byte[] userData = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(userData);
            }
            catch (CryptographicException ex)
            {
                System.Windows.MessageBox.Show($"Error decrypting password: {ex.Message}\nData might be corrupted or protected for a different user.", "Security Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return string.Empty;
            }
            catch (FormatException ex)
            {
                System.Windows.MessageBox.Show($"Error: Invalid password format. {ex.Message}", "Security Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return string.Empty;
            }
        }

        // Helper method to check if credentials exist without loading the full list
        public static bool AreCredentialsSaved()
        {
            return !string.IsNullOrEmpty(Credentials.Default.AllCredentials);
        }
    }
}