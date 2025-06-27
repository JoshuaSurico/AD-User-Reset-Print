using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        public ADSourcesWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Initialize last known location
            _lastKnownLocation = new Point(this.Left, this.Top);

            AdSourceList = [];
            LoadAdSources();

            UpdateButtonStates();

            this.DataContext = this;
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
            List<CredentialEntry> loaded = CredentialStorageService.LoadCredentials();
            foreach (var item in loaded)
            {
                AdSourceList.Add(item);
            }
        }

        private void SaveAdSources()
        {
            CredentialStorageService.SaveCredentials(AdSourceList.ToList());
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
            ADSourceConfigWindow configWindow = new()
            {
                Owner = this // Set owner to ADSourceManagerWindow
            };

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
            CredentialEntry selectedSource = (CredentialEntry)lbADSources.SelectedItem;

            if (selectedSource != null)
            {
                if (MessageBox.Show($"Are you sure you want to remove '{selectedSource.Domain} - {selectedSource.Username}'?",
                                    "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    AdSourceList.Remove(selectedSource); // Remove from the collection
                    SaveAdSources(); // Save the updated list to file
                    MessageBox.Show("AD Source removed.");
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
                // Pass the selected credential to the config window
                // Ensure CredentialEntry implements INotifyPropertyChanged for seamless updates
                // Or, if not, update the item in the ObservableCollection explicitly after dialog returns.
                ADSourceConfigWindow configWindow = new(selectedSource)
                {
                    Owner = this
                };

                bool? result = configWindow.ShowDialog();

                if (result == true) // If user clicked "Test Connection" and it succeeded
                {
                    // If CredentialEntry implements INotifyPropertyChanged, UI might update automatically.
                    // If not, or to be safe, you can replace the item to force UI refresh:
                    int index = AdSourceList.IndexOf(selectedSource);
                    if (index != -1)
                    {
                        AdSourceList.RemoveAt(index);
                        AdSourceList.Insert(index, configWindow.ResultCredential);
                    }
                    SaveAdSources(); // Save the updated list to file
                    MessageBox.Show("AD Source updated successfully!");
                }
            }
            else
            {
                MessageBox.Show("Please select an AD source to modify.");
            }
        }

        private void lbADSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (CredentialStorageService.AreCredentialsSaved())
            {
                this.DialogResult = true;
            }
            else
            {
                this.DialogResult = false;
            }
            this.Close();
        }
    }
}