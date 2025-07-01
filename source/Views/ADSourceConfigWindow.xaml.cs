using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Services.AD;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour ADSourceConfigWindow.xaml
    /// </summary>
    public partial class ADSourceConfigWindow : Window
    {
        private readonly ILoggingService _logger;
        private readonly IADSourceCheckService _adService;

        private ObservableCollection<string> _uiGroupNames;
        public CredentialEntry ResultCredential { get; private set; }
        private bool _isEditMode;
        private bool _lastTestSuccessful = false;
        private string _lastTestedDomain = string.Empty;
        private string _lastTestedUsername = string.Empty;
        private List<string> _lastTestedGroups = [];

        // This is the ONLY public constructor. It's used by the DI container.
        public ADSourceConfigWindow(ILoggingService logger, IADSourceCheckService adService)
        {
            InitializeComponent();
            _logger = logger;       // Assign injected logger
            _adService = adService; // Assign injected AD service

            this.Title = "Add New AD Source";
            _isEditMode = false;

            ResultCredential = new CredentialEntry("", "", "", []);  // Initialize with a default empty entry

            // Initialize collections and subscribe to events
            _uiGroupNames = [];
            LbGroups.ItemsSource = _uiGroupNames;
            _uiGroupNames.CollectionChanged += UiGroupNames_CollectionChanged;

            // Subscribe to the AD service output. This is correctly done here.
            _adService.OnOutputMessage += HandleOutputMessage;

            ResetInputRelatedUIState(); // Set initial UI state
        }

        /// <summary>
        /// Used to set the credential when opening the window in edit mode.
        /// This method is called *after* the window is created via DI.
        /// </summary>
        /// <param name="credential">The credential to pre-populate the UI with.</param>
        public void SetCredentialToModify(CredentialEntry credential)
        {
            if (credential == null) return;

            this.Title = "Edit AD Source";
            _isEditMode = true;
            ResultCredential?.Dispose();

            // We need to get the plaintext password to create a new object.
            // This is a secure operation handled inside the method.
            string plainTextPassword = ConvertSecureStringToPlainText(credential.Password);
            ResultCredential = new CredentialEntry(
                credential.Domain,
                credential.Username,
                plainTextPassword, // Pass the plaintext password
                credential.Groups
            );

            // Populate UI fields
            TxtbDomain.Text = ResultCredential.Domain;
            TxtbUsername.Text = ResultCredential.Username;
            PswbPassword.Clear(); // For security, never pre-fill password boxes. User must re-enter.

            // Populate group list
            _uiGroupNames.Clear();
            foreach (var group in ResultCredential.Groups)
            {
                _uiGroupNames.Add(group);
            }

            _lastTestSuccessful = true;
            _lastTestedDomain = ResultCredential.Domain;
            _lastTestedUsername = ResultCredential.Username;
            _lastTestedGroups = [.. ResultCredential.Groups];

            UpdateButtonStates();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void HandleOutputMessage(string message)
        {
            // Ensure this runs on the UI thread
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            txtOutput.Text = "";
            BtnTestConnection.IsEnabled = false;
            BtnTestConnection.Background = new SolidColorBrush(Colors.LightGray);

            string domain = TxtbDomain.Text;
            string username = TxtbUsername.Text;
            SecureString password = PswbPassword.SecurePassword; // Get SecureString directly from the password box
            List<string> targetGroups = [.. _uiGroupNames];

            // Strict check: Password box must not be empty for a test connection.
            // This prevents testing with an implicitly "unchanged" password, forcing user re-entry.
            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(username) || password.Length == 0)
            {
                txtOutput.AppendText("ERROR: Domain, Username, and Password cannot be empty for testing." + Environment.NewLine);
                BtnTestConnection.IsEnabled = true;
                BtnTestConnection.Background = new SolidColorBrush(Colors.Red);
                _lastTestSuccessful = false;
                UpdateButtonStates();
                password?.Dispose(); // Always dispose SecureString obtained from PswbPassword
                return;
            }

            PermissionCheckResult result = await _adService.RunPermissionCheckAsync(domain, username, password, targetGroups);

            _lastTestSuccessful = result.IsSuccessful;
            txtOutput.AppendText(Environment.NewLine);

            if (result.IsSuccessful)
            {
                txtOutput.AppendText("--- Overall Test Result: SUCCESS ---" + Environment.NewLine);
                BtnTestConnection.Background = new SolidColorBrush(Colors.Green);

                _lastTestedDomain = domain;
                _lastTestedUsername = username;
                _lastTestedGroups = [.. targetGroups];

                ResultCredential?.Dispose();

                // Create the new ResultCredential using the plaintext password from the box.
                // It's safe because the constructor immediately encrypts it.
                ResultCredential = new CredentialEntry(domain, username, PswbPassword.Password, targetGroups);
            }
            else
            {
                txtOutput.AppendText("--- Overall Test Result: FAILED ---" + Environment.NewLine);
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    txtOutput.AppendText($"Reason: {result.ErrorMessage}" + Environment.NewLine);
                }
                BtnTestConnection.Background = new SolidColorBrush(Colors.Red);
            }

            txtOutput.ScrollToEnd();
            BtnTestConnection.IsEnabled = true;
            UpdateButtonStates();

            password?.Dispose(); // Always dispose SecureString obtained from PswbPassword
        }

        // --- HELPER METHOD to securely convert SecureString ---
        private string ConvertSecureStringToPlainText(SecureString securePassword)
        {
            if (securePassword == null) return string.Empty;

            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToBSTR(securePassword);
                return Marshal.PtrToStringBSTR(valuePtr);
            }
            finally
            {
                if (valuePtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(valuePtr);
                }
            }
        }

        // Event handlers to trigger ResetInputRelatedUIState
        private void TxtbGroup_TextChanged(object sender, TextChangedEventArgs e) { ResetInputRelatedUIState(); }
        private void LbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e) { ResetInputRelatedUIState(); }
        private void Input_TextChanged(object sender, RoutedEventArgs e) { ResetInputRelatedUIState(); } // Handles TxtbDomain, TxtbUsername
        private void PswbPassword_PasswordChanged(object sender, RoutedEventArgs e) { ResetInputRelatedUIState(); } // Handles PswbPassword

        private void UpdateButtonStates()
        {
            btnAddGroup.IsEnabled = !string.IsNullOrWhiteSpace(txtbGroup.Text);
            btnRemoveGroup.IsEnabled = LbGroups.SelectedItem != null;

            bool inputsMatchLastTest = _lastTestSuccessful &&
                                        TxtbDomain.Text.Equals(_lastTestedDomain, StringComparison.OrdinalIgnoreCase) &&
                                        TxtbUsername.Text.Equals(_lastTestedUsername, StringComparison.OrdinalIgnoreCase) &&
                                        _uiGroupNames.SequenceEqual(_lastTestedGroups, StringComparer.OrdinalIgnoreCase);

            BtnConnection.IsEnabled = inputsMatchLastTest;
        }

        // Helper to compare SecureStrings safely
        private static bool CompareSecureString(SecureString s1, SecureString? s2)
        {
            if (s1 == null && s2 == null) return true;
            if (s1 == null || s2 == null) return false;
            if (s1.Length != s2.Length) return false;

            IntPtr bstr1 = IntPtr.Zero;
            IntPtr bstr2 = IntPtr.Zero;
            try
            {
                bstr1 = Marshal.SecureStringToBSTR(s1);
                bstr2 = Marshal.SecureStringToBSTR(s2);
                string str1 = Marshal.PtrToStringBSTR(bstr1)!;
                string str2 = Marshal.PtrToStringBSTR(bstr2)!;
                return str1.Equals(str2);
            }
            finally
            {
                if (bstr1 != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr1);
                if (bstr2 != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr2);
            }
        }

        private void btnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            string newGroupName = txtbGroup.Text.Trim();
            // Use case-insensitive comparison to check for existing groups
            if (!string.IsNullOrEmpty(newGroupName) && !_uiGroupNames.Contains(newGroupName, StringComparer.OrdinalIgnoreCase))
            {
                _uiGroupNames.Add(newGroupName);
                txtbGroup.Clear();
                ResetInputRelatedUIState(); // Input changed, reset test state
            }
            else if (string.IsNullOrEmpty(newGroupName))
            {
                MessageBox.Show("Please enter a group name.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (_uiGroupNames.Contains(newGroupName, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show("Group name already exists.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            UpdateButtonStates();
        }

        private void btnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (LbGroups.SelectedItem != null)
            {
                string selectedItem = LbGroups.SelectedItem.ToString();
                _uiGroupNames.Remove(selectedItem);
                ResetInputRelatedUIState(); // Input changed, reset test state
            }
            else
            {
                MessageBox.Show("Please select a group to remove.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            UpdateButtonStates();
        }

        private void BtnConnection_Click(object sender, RoutedEventArgs e)
        {
            // This check should already be handled by BtnConnection.IsEnabled, but it's a good safeguard.
            if (!BtnConnection.IsEnabled)
            {
                MessageBox.Show("Please run a successful 'Test Connection' with the current settings before connecting.", "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnConnection.Background = new SolidColorBrush(Colors.Red);
                return;
            }

            BtnConnection.Background = new SolidColorBrush(Colors.LightGreen);
            DialogResult = true; // Indicate success to the calling window
            this.Close();
        }

        // This is the "Cancel" or "Close" button. It should just close the window.
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Indicate that the operation was cancelled/not completed successfully
            this.Close();
        }

        private void ResetInputRelatedUIState()
        {
            _lastTestSuccessful = false;
            BtnTestConnection.Background = SystemColors.ControlBrush;
            txtOutput.Clear();
            UpdateButtonStates();
        }

        private void UiGroupNames_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ResetInputRelatedUIState();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ResultCredential?.Dispose();
        }
    }
}