using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Properties;
using AD_User_Reset_Print.Views;
using AD_User_Reset_Print.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        private List<CredentialEntry> credentialsList = []; // Initialize directly
        private readonly LoginCheckService _adService;
        private readonly CredentialStorageService _credentialStorageService;

        public Login()
        {
            InitializeComponent();

            _adService = new LoginCheckService();
            _adService.OnOutputMessage += HandleOutputMessage;
            _adService.OnErrorOccurred += HandleErrorMessage;

            _credentialStorageService = new CredentialStorageService(); // Instantiate the new service

            // Load credentials when the Login window initializes
            LoadCredentials();

            // Populate text boxes if there are saved credentials for convenience
            if (credentialsList.Count != 0)
            {
                var defaultCred = credentialsList.FirstOrDefault();
                if (defaultCred != null)
                {
                    txtbDomain.Text = defaultCred.Domain;
                    txtbUsername.Text = defaultCred.Username;
                }
            }
        }

        private void HandleOutputMessage(string message)
        {
            // Ensure UI updates are on the UI thread
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        private void HandleErrorMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText("ERROR: " + message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        private void LoadCredentials()
        {
            credentialsList = CredentialStorageService.LoadCredentials();
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            txtOutput.Text = ""; // Clear previous output
            BtnTestConnection.IsEnabled = false; // Disable button during check

            string domain = txtbDomain.Text;
            string username = txtbUsername.Text;
            string password = pswbPassword.Password;
            List<string> targetGroups = ["UUS_GYREN"];

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                HandleErrorMessage("Domain, Username and Password cannot be empty.");
                BtnTestConnection.IsEnabled = true;
                return;
            }

            PermissionCheckResult result = await _adService.RunPermissionCheckAsync(domain, username, password, targetGroups);

            if (result.IsSuccessful)
            {
                HandleOutputMessage("\nLogin and Permission check finished successfully.");

                // Use the new service to save/clear credentials
                CredentialEntry currentCredential = new CredentialEntry
                {
                    Domain = domain,
                    Username = username,
                    Password = password // This is the unprotected password
                };
                CredentialStorageService.SaveCredential(currentCredential);

                DialogResult = true;
                this.Close();
            }
            else
            {
                HandleErrorMessage($"\nLogin or Permission check failed: {result.ErrorMessage}");
                DialogResult = false;
            }

            BtnTestConnection.IsEnabled = true; // Re-enable button
        }

        // Add an event handler for the window closing
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If DialogResult is not true, it means login was not successful or window was closed manually.
            // In App.xaml.cs, we will check this to decide whether to shut down the app.
        }
    }
}