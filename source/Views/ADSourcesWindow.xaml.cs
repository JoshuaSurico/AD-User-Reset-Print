using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Logique d'interaction pour ADSourcesWindow.xaml
    /// </summary>
    public partial class ADSourcesWindow : Window
    {
        // Store the last known location of THIS window to calculate delta
        private Point _lastKnownLocation;
        public ObservableCollection<CredentialEntry> AdSourceList { get; set; }

        private readonly ILoggingService _logger;
        private readonly ICredentialStorageService _credentialStorageService;
        private readonly IServiceProvider _serviceProvider;

        public ADSourcesWindow(ILoggingService logger, ICredentialStorageService credentialStorageService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Assign injected services to readonly fields
            _logger = logger;
            _credentialStorageService = credentialStorageService;
            _serviceProvider = serviceProvider; // Assign the injected service provider

            // Initialize last known location after window position is determined
            _lastKnownLocation = new Point(this.Left, this.Top);

            AdSourceList = [];
            LoadAdSources();

            UpdateButtonStates();

            this.DataContext = this;
            this.Closing += ADSourcesWindow_Closing;
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            // Only proceed if this window actually has an owner
            if (this.Owner != null)
            {
                // Calculate how much THIS window has moved
                double deltaX = this.Left - _lastKnownLocation.X;
                double deltaY = this.Top - _lastKnownLocation.Y;

                // Update _lastKnownLocation for the next move event
                _lastKnownLocation = new Point(this.Left, this.Top);

                // Move the Owner window by the same delta
                this.Owner.Left += deltaX;
                this.Owner.Top += deltaY;
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

        private void LoadAdSources()
        {
            AdSourceList.Clear();
            List<CredentialEntry> loaded = _credentialStorageService.LoadCredentials();
            foreach (var item in loaded)
            {
                AdSourceList.Add(item);
            }
        }

        private void SaveAdSources()
        {
            _credentialStorageService.SaveCredentials([.. AdSourceList]);
        }

        // Central method to update button states
        private void UpdateButtonStates()
        {
            // Activate btnRemove and btnModify only when an item is selected in lbGroups
            btnRemove.IsEnabled = lbADSources.SelectedItem != null;
            btnModify.IsEnabled = lbADSources.SelectedItem != null;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            ADSourceConfigWindow configWindow = _serviceProvider.GetRequiredService<ADSourceConfigWindow>();
            configWindow.Owner = this;

            bool? result = configWindow.ShowDialog();

            if (result == true)
            {
                CredentialEntry newEntry = configWindow.ResultCredential;
                if (newEntry != null)
                {
                    AdSourceList.Add(newEntry); // Add the new entry to the collection
                    SaveAdSources();
                    MessageBox.Show("AD Source added successfully!");
                }
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            CredentialEntry? selectedSource = (CredentialEntry?)lbADSources.SelectedItem; // Changed to nullable

            if (selectedSource != null)
            {
                // Using CustomMessageBox instead of System.Windows.MessageBox
                CustomMessageBox confirmBox = new(
                    $"Are you sure you want to remove '{selectedSource.Domain} - {selectedSource.Username}'?",
                    "Confirm Removal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                )
                {
                    Owner = this // Set owner for the message box
                };

                if (confirmBox.ShowDialog() == true) // CustomMessageBox returns true for Yes
                {
                    AdSourceList.Remove(selectedSource); // Remove from the collection
                    SaveAdSources(); // Save the updated list to file
                    MessageBox.Show("AD Source removed.");
                    UpdateButtonStates(); // Update buttons after removal
                }
            }
            else
            {
                MessageBox.Show("Please select an AD source to remove.");
            }
        }

        private void BtnModify_Click(object sender, RoutedEventArgs e)
        {
            CredentialEntry selectedSource = (CredentialEntry)lbADSources.SelectedItem;

            if (selectedSource != null)
            {
                // 1. Resolve the window from the service provider
                ADSourceConfigWindow configWindow = _serviceProvider.GetRequiredService<ADSourceConfigWindow>();
                // 2. Set the owner
                configWindow.Owner = this;
                // 3. Pass the specific data using the new method
                configWindow.SetCredentialToModify(selectedSource);

                bool? result = configWindow.ShowDialog();

                if (result == true)
                {
                    CredentialEntry? modifiedEntry = configWindow.ResultCredential;
                    if (modifiedEntry != null)
                    {
                        var originalEntry = AdSourceList.FirstOrDefault(c => c == selectedSource); // Find the exact instance
                        if (originalEntry != null)
                        {
                            int index = AdSourceList.IndexOf(originalEntry);
                            if (index != -1)
                            {
                                AdSourceList[index] = modifiedEntry;
                            }
                        }
                    }
                    SaveAdSources();
                    MessageBox.Show("AD Source updated successfully!");
                }
            }
            else
            {
                MessageBox.Show("Please select an AD source to modify.");
            }
        }

        private void LbADSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Handles the window's Closing event to prompt the user if no credentials are saved.
        /// </summary>
        private void ADSourcesWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // If the user is actively saving, don't interfere
            if (_credentialStorageService.AreCredentialsSaved())
            {
                this.DialogResult = true; // Signal success if credentials are saved
                return;
            }

            // If no credentials are saved, show the warning message box
            _logger.Log("ADSourcesWindow is closing with no credentials saved. Prompting user.", LogLevel.Warning);

            CustomMessageBox customMsgBox = new(
                "Aucune information d'identification AD n'a été configurée. L'application nécessite des informations d'identification pour fonctionner correctement.\n\n" +
                "Voulez-vous les configurer maintenant ? (Cliquez sur 'Non' pour quitter l'application)",
                "Configuration Requise",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            )
            {
                Owner = this // Set owner for the message box
            };

            bool? customDialogResult = customMsgBox.ShowDialog();

            if (customDialogResult == true) // User clicked 'Oui' (Yes)
            {
                _logger.Log("User chose to re-configure AD credentials.", LogLevel.Info);
                e.Cancel = true; // Cancel the closing, keeping the window open
            }
            else // User clicked 'Non' (No) or closed the custom dialog
            {
                _logger.Log("User chose to exit application due to unconfigured AD credentials.", LogLevel.Info);
                this.DialogResult = false; // Signal failure to the caller
            }
        }
    }
}