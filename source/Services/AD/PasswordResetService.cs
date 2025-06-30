// File: AD_User_Reset_Print.Services.AD/PasswordResetService.cs
using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services; // For CredentialStorageService
using System;
using System.DirectoryServices.AccountManagement; // For AD interaction
using System.Globalization; // Needed for CultureInfo
using System.Linq; // For .Any() and .FirstOrDefault()
using System.Security; // For SecureString
using System.Windows; // For MessageBox

namespace AD_User_Reset_Print.Services.AD
{
    public static class PasswordResetService
    {
        /// <summary>
        /// Resets a user's password in Active Directory using configured administrative credentials
        /// and returns the generated temporary password.
        /// </summary>
        /// <param name="user">The User object for whom to reset the password.</param>
        /// <returns>The newly generated temporary password, or null if the reset failed.</returns>
        public static string Reset(User user)
        {
            if (user == null)
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la réinitialisation du mot de passe.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // 1. Load credentials from storage
            List<CredentialEntry> credentials = CredentialStorageService.LoadCredentials();
            CredentialEntry adminCredential = null;

            // Logic to select the appropriate admin credential.
            // In a more complex app, you might have a UI to let the user pick,
            // or match by domain. For simplicity, we'll try to match by domain, then take the first available.
            if (credentials.Any())
            {
                adminCredential = credentials.FirstOrDefault(c => string.Equals(c.Domain, user.Domain, StringComparison.OrdinalIgnoreCase));
                if (adminCredential == null)
                {
                    // If no specific domain credential found, use the first one as a fallback.
                    // This might not always be appropriate if you have multiple domains.
                    adminCredential = credentials.First();
                    MessageBox.Show($"Aucun identifiant spécifique trouvé pour le domaine '{user.Domain}'. Utilisation du premier identifiant disponible pour '{adminCredential.Domain}'.", "Avertissement de créancier", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            if (adminCredential == null)
            {
                MessageBox.Show("Aucun identifiant administrateur n'est configuré pour se connecter à Active Directory. Veuillez configurer les identifiants.", "Configuration Requise", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            // Generate the temporary password based on today's date
            string newTempPassword = GenerateTempPasswordForDate(DateTime.Today);

            try
            {
                // Perform the actual password reset using the internal helper method
                bool success = ResetUserPasswordInternal(user, newTempPassword, adminCredential);

                if (success)
                {
                    MessageBox.Show($"Le mot de passe pour '{user.DisplayName}' ({user.SAMAccountName}) a été réinitialisé.",
                                    "Réinitialisation du mot de passe réussie", MessageBoxButton.OK, MessageBoxImage.Information);
                    MessageBox.Show($"Nouveau mot de passe temporaire : {newTempPassword}\n\nVeuillez communiquer ce mot de passe à l'utilisateur et assurez-vous qu'il le change lors de sa première connexion.",
                                    "Mot de passe temporaire", MessageBoxButton.OK, MessageBoxImage.Information);
                    return newTempPassword;
                }
                else
                {
                    // Error message would have been shown by ResetUserPasswordInternal
                    return null;
                }
            }
            catch (Exception ex)
            {
                // This catch block is for errors *before* or *after* the AD interaction,
                // or if ResetUserPasswordInternal itself throws an unhandled exception.
                MessageBox.Show($"Une erreur est survenue lors de la réinitialisation du mot de passe pour {user.DisplayName}: {ex.Message}", "Erreur de réinitialisation", MessageBoxButton.OK, MessageBoxImage.Error);
                // Log the exception
                return null;
            }
        }

        /// <summary>
        /// Resets a user's password in Active Directory using specified administrative credentials.
        /// This is an internal method, intended to be called by the public Reset method.
        /// </summary>
        /// <param name="user">The user object whose password needs to be reset.</param>
        /// <param name="newPassword">The new temporary password to set.</param>
        /// <param name="adminCredential">The CredentialEntry containing the admin username and password.</param>
        /// <param name="expirePassword">True to force user to change password on next logon.</param>
        /// <returns>True if the password reset was successful, false otherwise.</returns>
        private static bool ResetUserPasswordInternal(User user, string newPassword, CredentialEntry adminCredential, bool expirePassword = true)
        {
            if (user == null || adminCredential == null)
            {
                MessageBox.Show("Identifiants utilisateur ou administrateur invalides fournis pour l'opération AD.", "Erreur AD", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, adminCredential.Domain, adminCredential.Username, adminCredential.Password))
                {
                    UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, user.SAMAccountName);

                    if (userPrincipal != null)
                    {
                        userPrincipal.SetPassword(newPassword);
                        if (expirePassword)
                        {
                            userPrincipal.ExpirePasswordNow();
                        }
                        userPrincipal.Save();
                        return true;
                    }
                    else
                    {
                        MessageBox.Show($"Utilisateur '{user.SAMAccountName}' introuvable dans le domaine '{user.Domain}'.", "Erreur AD", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
            }
            catch (PrincipalServerDownException ex)
            {
                MessageBox.Show($"Impossible de se connecter au contrôleur de domaine pour '{adminCredential.Domain}'. Vérifiez la connectivité réseau et le nom du domaine. Détails: {ex.Message}", "Erreur de connexion AD", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Accès refusé. Les identifiants fournis n'ont pas la permission de réinitialiser le mot de passe pour l'utilisateur '{user.SAMAccountName}'. Détails: {ex.Message}", "Erreur d'autorisation AD", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Une erreur inattendue est survenue lors de la réinitialisation du mot de passe AD pour '{user.SAMAccountName}'. Détails: {ex.Message}", "Erreur AD", MessageBoxButton.OK, MessageBoxImage.Error);
                // Log the full exception for debugging
                return false;
            }
        }

        /// <summary>
        /// Retrieves the LastPasswordSet date for a user from Active Directory using specified administrative credentials.
        /// </summary>
        /// <param name="user">The user object to query.</param>
        /// <returns>The DateTime of the last password set, or null if not found or an error occurs.</returns>
        public static DateTime? GetLastPasswordSetDate(User user)
        {
            if (user == null)
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la récupération de la date du dernier mot de passe.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // 1. Load credentials from storage
            List<CredentialEntry> credentials = CredentialStorageService.LoadCredentials();
            CredentialEntry adminCredential = null;
            if (credentials.Count != 0)
            {
                adminCredential = credentials.FirstOrDefault(c => string.Equals(c.Domain, user.Domain, StringComparison.OrdinalIgnoreCase)) ?? credentials.First();
            }

            if (adminCredential == null)
            {
                MessageBox.Show("Aucun identifiant administrateur n'est configuré pour se connecter à " + user.Domain + ". Impossible de récupérer la date de dernier mot de passe.", "Configuration Requise", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, adminCredential.Domain, adminCredential.Username, adminCredential.Password))
                {
                    UserPrincipal userPrincipal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, user.SAMAccountName);
                    if (userPrincipal != null)
                    {
                        return userPrincipal.LastPasswordSet;
                    }
                }
            }
            catch (PrincipalServerDownException ex)
            {
                MessageBox.Show($"Impossible de se connecter au contrôleur de domaine pour '{adminCredential.Domain}'. Détails: {ex.Message}", "Erreur de connexion AD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Accès refusé. Les identifiants fournis n'ont pas la permission de lire les informations de l'utilisateur '{user.SAMAccountName}'. Détails: {ex.Message}", "Erreur d'autorisation AD", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Une erreur inattendue est survenue lors de la récupération de la date de dernier mot de passe pour '{user.SAMAccountName}'. Détails: {ex.Message}", "Erreur AD", MessageBoxButton.OK, MessageBoxImage.Error);
                // Log the full exception
            }
            return null;
        }


        /// <summary>
        /// Generates a temporary password in the format "Gyre@DD.MM.YYYY" for a given date.
        /// </summary>
        /// <param name="date">The date to use in the password.</param>
        /// <returns>The generated temporary password.</returns>
        public static string GenerateTempPasswordForDate(DateTime date)
        {
            string formattedDate = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            return $"Gyre@{formattedDate}";
        }
    }
}