using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Properties;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Logique d'interaction pour ADSourceConfigWindow.xaml
    /// </summary>
    public partial class ADSourceConfigWindow : Window
    {
        // Renamed to avoid confusion, this is the UI-bound collection
        private ObservableCollection<string> _uiGroupNames = [];
        private ADSourceCheckService _adService;

        public CredentialEntry ResultCredential { get; private set; } // This is the credential we are working on
        private bool _isEditMode;

        // Keep track of the last test result
        private bool _lastTestSuccessful = false;

        // Store the last successfully tested credentials (these should mirror ResultCredential once test is successful)
        private string _lastTestedDomain = string.Empty;
        private string _lastTestedUsername = string.Empty;
        private string _lastTestedPassword = string.Empty;
        private List<string> _lastTestedGroups = [];

        // Constructor for ADDING a new AD Source
        public ADSourceConfigWindow()
        {
            InitializeComponent();
            this.Title = "Add New AD Source";
            _isEditMode = false;
            ResultCredential = new CredentialEntry(); // Initialize a brand new, empty credential
            InitializeCommonLogic(); // Call common setup after ResultCredential is initialized
        }

        // Constructor for EDITING an existing AD Source
        public ADSourceConfigWindow(CredentialEntry existingCredential)
        {
            InitializeComponent(); // Initialize UI components first
            this.Title = "Edit AD Source";
            _isEditMode = true;

            // IMPORTANT: Create a *copy* of the existing credential.
            // We don't want to modify the original 'existingCredential' directly
            // until the user confirms by clicking "Connect/Save".
            ResultCredential = new CredentialEntry(
                existingCredential.Domain,
                existingCredential.Username,
                existingCredential.Password,
                existingCredential.Groups.ToList() // Pass a copy of the group list
            );

            InitializeCommonLogic(); // Call common setup AFTER ResultCredential is populated
                                     // This ensures _uiGroupNames gets populated from ResultCredential.Groups

            // Populate UI fields from the copied ResultCredential
            TxtbDomain.Text = ResultCredential.Domain;
            TxtbUsername.Text = ResultCredential.Username;
            PswbPassword.Password = ResultCredential.Password; // Populate password (consider security implications for displaying it)

            // In edit mode, assume existing credential is valid initially for connection
            // User can re-test if they modify credentials.
            // Set _lastTestSuccessful based on the initial state, perhaps.
            // If you want to force re-test on edit, then remove these assignments.
            // For now, let's assume if it exists, it was once successful.
            _lastTestSuccessful = true; // Or false if you always want a re-test on edit
            _lastTestedDomain = ResultCredential.Domain;
            _lastTestedUsername = ResultCredential.Username;
            _lastTestedPassword = ResultCredential.Password;
            _lastTestedGroups = [.. ResultCredential.Groups]; // Copy of the groups from the *copied* credential

            UpdateButtonStates();
        }

        private void InitializeCommonLogic()
        {
            // Set the ItemsSource for the ListBox.
            // This collection will be manipulated by Add/Remove Group buttons.
            // Crucially, it should be initialized from ResultCredential.Groups.
            // Ensure ResultCredential is already set by the time this is called.
            _uiGroupNames = new ObservableCollection<string>(ResultCredential.Groups); // Initialize from ResultCredential's groups
            LbGroups.ItemsSource = _uiGroupNames; // Bind to this UI-specific collection

            UpdateButtonStates(); // Initial state of buttons

            _adService = new ADSourceCheckService();
            _adService.OnOutputMessage += HandleOutputMessage;
            _uiGroupNames.CollectionChanged += UiGroupNames_CollectionChanged;
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
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            txtOutput.Text = ""; // Clear previous output
            BtnTestConnection.IsEnabled = false;
            BtnTestConnection.Background = new SolidColorBrush(Colors.LightGray); // Indicate testing in progress

            string domain = TxtbDomain.Text;
            string username = TxtbUsername.Text;
            string password = PswbPassword.Password;
            List<string> targetGroups = [.. _uiGroupNames];

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                // This is an immediate UI validation error, not an AD service error.
                // It's fine to directly set output here as the service isn't even called.
                txtOutput.AppendText("ERROR: Domain, Username, and Password cannot be empty." + Environment.NewLine);
                BtnTestConnection.IsEnabled = true;
                BtnTestConnection.Background = new SolidColorBrush(Colors.Red); // Set color to red for failure
                _lastTestSuccessful = false;
                UpdateButtonStates();
                return;
            }

            PermissionCheckResult result = await _adService.RunPermissionCheckAsync(domain, username, password, targetGroups);

            _lastTestSuccessful = result.IsSuccessful;

            // Add a blank line for visual separation before the final summary
            txtOutput.AppendText(Environment.NewLine);

            if (result.IsSuccessful)
            {
                txtOutput.AppendText("--- Overall Test Result: SUCCESS ---" + Environment.NewLine);
                txtOutput.AppendText("Login and Permission check completed successfully." + Environment.NewLine);
                BtnTestConnection.Background = new SolidColorBrush(Colors.Green); // Set color to green for success

                // Store the successfully tested credentials
                _lastTestedDomain = domain;
                _lastTestedUsername = username;
                _lastTestedPassword = password;
                _lastTestedGroups = [.. targetGroups];

                ResultCredential = new CredentialEntry
                {
                    Domain = domain,
                    Username = username,
                    Password = password,
                    Groups = new ObservableCollection<string>(targetGroups)
                };
            }
            else
            {
                txtOutput.AppendText("--- Overall Test Result: FAILED ---" + Environment.NewLine);
                // Only append the reason if there is one, to avoid "Reason: " if no specific message.
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    txtOutput.AppendText($"Reason: {result.ErrorMessage}" + Environment.NewLine);
                }
                BtnTestConnection.Background = new SolidColorBrush(Colors.Red); // Set color to red for failure
            }

            txtOutput.ScrollToEnd();
            BtnTestConnection.IsEnabled = true;
            UpdateButtonStates();
        }

        private void txtbGroup_TextChanged(object sender, TextChangedEventArgs e) { UpdateButtonStates(); }
        private void lbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e) { UpdateButtonStates(); }

        private void UpdateButtonStates()
        {
            btnAddGroup.IsEnabled = !string.IsNullOrWhiteSpace(txtbGroup.Text);
            btnRemoveGroup.IsEnabled = LbGroups.SelectedItem != null;

            BtnConnection.IsEnabled = _lastTestSuccessful;
        }

        private void btnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            string newGroupName = txtbGroup.Text.Trim();
            if (!string.IsNullOrEmpty(newGroupName) && !_uiGroupNames.Contains(newGroupName))
            {
                _uiGroupNames.Add(newGroupName);
                txtbGroup.Clear();
                // If groups change, the previous test result might be invalid
                _lastTestSuccessful = false;
                BtnTestConnection.Background = new SolidColorBrush(Colors.LightGray); // Reset test color
                txtOutput.Text = ""; // Clear output on input change
            }
            else if (string.IsNullOrEmpty(newGroupName))
            {
                MessageBox.Show("Please enter a group name.");
            }
            else if (_uiGroupNames.Contains(newGroupName))
            {
                MessageBox.Show("Group name already exists.");
            }
            UpdateButtonStates();
        }

        private void btnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (LbGroups.SelectedItem != null)
            {
                string selectedItem = LbGroups.SelectedItem.ToString();
                _uiGroupNames.Remove(selectedItem);
                // If groups change, the previous test result might be invalid
                _lastTestSuccessful = false;
                BtnTestConnection.Background = new SolidColorBrush(Colors.LightGray); // Reset test color
                txtOutput.Text = ""; // Clear output on input change
            }
            else
            {
                MessageBox.Show("Please select a group to remove.");
            }
            UpdateButtonStates();
        }

        private void BtnConnection_Click(object sender, RoutedEventArgs e)
        {
            // Re-verify that the current inputs match the last successful test
            string currentDomain = TxtbDomain.Text;
            string currentUsername = TxtbUsername.Text;
            string currentPassword = PswbPassword.Password;
            List<string> currentGroups = [.. _uiGroupNames];

            if (!_lastTestSuccessful ||
                currentDomain != _lastTestedDomain ||
                currentUsername != _lastTestedUsername ||
                currentPassword != _lastTestedPassword ||
                !currentGroups.SequenceEqual(_lastTestedGroups)) // Check if groups are identical
            {
                MessageBox.Show("Please run a successful 'Test Connection' with the current settings before connecting.", "Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnConnection.Background = new SolidColorBrush(Colors.Red); // Indicate error
                return;
            }

            // If we reach here, it means the last test was successful and the credentials haven't changed.
            BtnConnection.Background = new SolidColorBrush(Colors.LightGreen);
            DialogResult = true; // Indicate success to the calling window
            this.Close();
        }

        private void InputChanged()
        {
            _lastTestSuccessful = false;
            UpdateButtonStates();
            // Clear the test result indicator immediately
            BtnTestConnection.Background = new SolidColorBrush(Colors.LightGray);
            txtOutput.Text = ""; // Clear output on input change
        }

        private void TxtbDomain_TextChanged(object sender, TextChangedEventArgs e) { InputChanged(); }

        private void TxtbUsername_TextChanged(object sender, TextChangedEventArgs e) { InputChanged(); }

        private void PswbPassword_PasswordChanged(object sender, RoutedEventArgs e) { InputChanged(); }
        
        private void UiGroupNames_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) { InputChanged(); }
    }
}