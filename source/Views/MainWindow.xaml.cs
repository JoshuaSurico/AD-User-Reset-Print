using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Services.AD;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<User> Users { get; set; }
        public User SelectedUser { get; set; }
        private string? _lastGeneratedPasswordForImmediatePrint;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            Users = [];
            SelectedUser = new();

            // For demo, let's ensure some credentials are saved (you'd do this in a setup screen)
            if (!CredentialStorageService.AreCredentialsSaved())
            {
                List<CredentialEntry> demoCredentials = new List<CredentialEntry>
                {
                    // This is just for demonstration purposes. In a real app, this would be set up via a secure UI.
                    new("domain.com", "admin_user", "AdminPassword123!", new List<string> { "Domain Admins" })
                };
                CredentialStorageService.SaveCredentials(demoCredentials);
                MessageBox.Show("Démo : Identifiants administrateur enregistrés. Veuillez les configurer via l'interface utilisateur pour une utilisation réelle.", "Configuration Initiale", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Event handler for dragging the custom window
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnADSettings_Click(object sender, RoutedEventArgs e)
        {
            ADSourcesWindow _adSources = new() { Owner = this };
            _adSources.ShowDialog();
        }

        private void BtnUISettings_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string githubUrl = "https://github.com/JoshuaSurico/AD-User-Reset-Print";

                Process.Start(new ProcessStartInfo(githubUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // Handle potential errors, e.g., if no default browser is set
                MessageBox.Show($"Could not open help page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnResetPsw_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser != null)
            {
                // Call the Reset method from the AD.PasswordResetService
                _lastGeneratedPasswordForImmediatePrint = AD_User_Reset_Print.Services.AD.PasswordResetService.Reset(SelectedUser);
                // The messages are now handled by PasswordResetService.Reset
            }
            else
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la réinitialisation.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour l'impression du mot de passe.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string passwordToPrint = null;
            if (_lastGeneratedPasswordForImmediatePrint != null)
            {
                passwordToPrint = _lastGeneratedPasswordForImmediatePrint;
            }
            else
            {
                passwordToPrint = "DemoP@ssw0rd123!"; // Generic demo password
            }

            // Now, show the print preview
            PrintService.ShowPrintPreview(SelectedUser, passwordToPrint);
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            SynchronizeUserService syncService = new SynchronizeUserService();
            List<User> newUsers = syncService.Sync();
            Users.Clear(); // Clear existing users
            foreach (var user in newUsers)
            {
                Users.Add(user); // Add newly fetched users
            }
            MessageBox.Show("Synchronization (demo) complete!", "Sync Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            CredentialStorageService.ClearAllCredentials();
        }


        // Single Click - Update SelectedUser
        private void LbUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedUser = (User)lbUsers.SelectedItem;
            if (SelectedUser != null)
            {
                _lastGeneratedPasswordForImmediatePrint = null;
            }
        }

        // Double Click - Open SingleAccountDetails window
        private void LbUsers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lbUsers.SelectedItem is User selectedUser)
            {
                SingleAccountDetails singleAccountDetailsWindow = new(selectedUser);
                singleAccountDetailsWindow.Show();
            }
        }

        // Right Click - Show Context Menu (already handled by XAML placement)
        private void ListBoxItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                SelectedUser = (User)item.DataContext;
                _lastGeneratedPasswordForImmediatePrint = null;
            }
        }

        // Context Menu Item Clicks
        private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser != null)
            {
                _lastGeneratedPasswordForImmediatePrint = AD_User_Reset_Print.Services.AD.PasswordResetService.Reset(SelectedUser);
            }
            else
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la réinitialisation.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PrintMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour l'impression.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string passwordToPrint = null;
            // For print menu item, you might still want to try to get the actual AD date,
            // or just use the last generated one for consistency with BtnPrint_Click demo.
            // I'll keep the original logic for this one for now, as it attempts AD lookup.
            DateTime? lastPasswordSetDate = AD_User_Reset_Print.Services.AD.PasswordResetService.GetLastPasswordSetDate(SelectedUser);

            if (lastPasswordSetDate.HasValue)
            {
                passwordToPrint = AD_User_Reset_Print.Services.AD.PasswordResetService.GenerateTempPasswordForDate(lastPasswordSetDate.Value);
            }
            else if (_lastGeneratedPasswordForImmediatePrint != null && SelectedUser == lbUsers.SelectedItem)
            {
                passwordToPrint = _lastGeneratedPasswordForImmediatePrint;
            }
            else
            {
                // Fallback to demo password if AD lookup fails and no recent reset.
                passwordToPrint = "DemoP@ssw0rd123!"; // Using a generic demo password here
                MessageBox.Show("Impossible de déterminer le mot de passe temporaire. " +
                                "Utilisation d'un mot de passe de démonstration pour l'aperçu.",
                                "Avertissement", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Now, show the print preview
            PrintService.ShowPrintPreview(SelectedUser, passwordToPrint);
        }

        private void ResetAndPrintMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser != null)
            {
                _lastGeneratedPasswordForImmediatePrint = AD_User_Reset_Print.Services.AD.PasswordResetService.Reset(SelectedUser);

                if (_lastGeneratedPasswordForImmediatePrint != null)
                {
                    // For "Reset and Print", you probably want direct printing without another preview step
                    PrintService.CreatePrintDocumentDirect(SelectedUser, _lastGeneratedPasswordForImmediatePrint);
                }
            }
            else
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la réinitialisation et l'impression.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}