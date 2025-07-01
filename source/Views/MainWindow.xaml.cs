using AD_User_Reset_Print.Models;
using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Services.AD;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AD_User_Reset_Print.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<User> Users { get; set; }
        public User SelectedUser { get; set; } = new User(); // Initialize to avoid null, or make nullable
        private string? _lastGeneratedPasswordForImmediatePrint;

        private readonly ILoggingService _loggingService;
        private readonly IPasswordResetService _passwordResetService;
        private readonly ICredentialStorageService _credentialStorageService;
        private readonly ISynchronizeUserService _synchronizeUserService;
        private readonly IServiceProvider _serviceProvider; // To resolve other windows

        public MainWindow(ILoggingService loggingService, IPasswordResetService passwordResetService, ICredentialStorageService credentialStorageService, ISynchronizeUserService synchronizeUserService, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            this.DataContext = this;
            Users = [];

            // Assign injected services to readonly fields
            _loggingService = loggingService;
            _passwordResetService = passwordResetService;
            _credentialStorageService = credentialStorageService;
            _synchronizeUserService = synchronizeUserService;
            _serviceProvider = serviceProvider;

            // This ensures our LoggingService is initialized on startup.
            _loggingService.Log("Application starting up.");

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            _loggingService.Log("Attempting to load user list from cache.");
            if (File.Exists(AppSettings.UserListFilePath))
            {
                var usersFromFile = JsonManagerService.ReadFromJson<User>(AppSettings.UserListFilePath);
                foreach (var user in usersFromFile)
                {
                    Users.Add(user);
                }
                DateTime lastSyncTime = File.GetLastWriteTime(AppSettings.UserListFilePath);
                lblUserSync.Content = $"Last sync: {lastSyncTime:dd.MM.yyyy HH:mm}";
                _loggingService.Log($"Loaded {usersFromFile.Count} users from {AppSettings.UserListFilePath}");
            }
            else
            {
                lblUserSync.Content = "Not yet synchronized. Please click Sync.";
                _loggingService.Log("User list cache not found.");
            }
        }

        // Event handler for dragging the custom window
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnADSettings_Click(object sender, RoutedEventArgs e)
        {
            // Resolve ADSourcesWindow from the service provider
            var adSourcesWindow = _serviceProvider.GetRequiredService<ADSourcesWindow>();
            adSourcesWindow.Owner = this;
            adSourcesWindow.ShowDialog();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Settings window
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
                MessageBox.Show($"Could not open help page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _loggingService.Log($"Failed to open help page: {ex.Message}", LogLevel.Error);
            }
        }

        private void BtnResetPsw_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("No user selected.", "Action Canceled", MessageBoxButton.OK, MessageBoxImage.Information);
                _loggingService.Log("Password reset attempted with no user selected.", LogLevel.Warning);
                return;
            }

            _lastGeneratedPasswordForImmediatePrint = _passwordResetService.Reset(SelectedUser);

            if (_lastGeneratedPasswordForImmediatePrint == null)
            {
                MessageBox.Show($"Failed to reset password for {SelectedUser.DisplayName}. Please check the logs for details.", "Reset Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show($"Password has been reset successfully for {SelectedUser.DisplayName}.\nThe new temporary password is: {_lastGeneratedPasswordForImmediatePrint}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("No user selected.", "Action Canceled", MessageBoxButton.OK, MessageBoxImage.Information);
                _loggingService.Log("Print attempted with no user selected.", LogLevel.Warning);
                return;
            }

            // If a password was just reset, use that. Otherwise, generate a "dummy" one for the preview.
            string passwordToPrint = _lastGeneratedPasswordForImmediatePrint ?? "Password-Not-Reset";
            PrintService.ShowPrintPreview(SelectedUser, passwordToPrint);
            _loggingService.Log($"Print preview shown for {SelectedUser.DisplayName}. Password: {(_lastGeneratedPasswordForImmediatePrint != null ? "Generated" : "Not Reset")}", LogLevel.Info);
        }

        private async void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            _loggingService.ResetErrorFlag();
            btnSync.IsEnabled = false;
            pbUserSync.Value = 0;
            Users.Clear();

            var progress = new Progress<ProgressReport>(report =>
            {
                pbUserSync.Value = report.PercentComplete;
                lblUserSync.Content = report.CurrentActivity;
            });

            _loggingService.Log("Synchronization started by user.");

            // Use the injected synchronizeUserService
            await Task.Run(() => _synchronizeUserService.Sync(progress));

            // After sync, reload from the definitive source file.
            var newUsers = JsonManagerService.ReadFromJson<User>(AppSettings.UserListFilePath);
            foreach (var user in newUsers)
            {
                Users.Add(user);
            }

            if (_loggingService.HasErrors)
            {
                lblUserSync.Content = "Sync completed with errors (see logs)";
                MessageBox.Show("Synchronization completed with errors. Click 'View Logs' for details.", "Sync Issues", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.Log("Synchronization completed with errors.", LogLevel.Warning);
            }
            else
            {
                DateTime lastSyncTime = DateTime.Now;
                lblUserSync.Content = $"Sync successful: {lastSyncTime:dd.MM.yyyy HH:mm} ({Users.Count} users)";
                _loggingService.Log("Synchronization completed successfully.", LogLevel.Info);
            }

            btnSync.IsEnabled = true;
        }

        private LogsWindow? _currentLogWindow; // Declare a nullable field to hold the instance

        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            // Check if the window instance doesn't exist OR if it was created but has since been closed by the user.
            // Using _currentLogWindow to check if it's open, rather than _logWindowInstance which was removed.
            if (_currentLogWindow == null || !(_currentLogWindow.IsLoaded)) // Check if it's not loaded
            {
                // Resolve a new LogsWindow instance from the service provider
                _currentLogWindow = _serviceProvider.GetRequiredService<LogsWindow>();
                _currentLogWindow.Owner = this; // Set the owner
                _currentLogWindow.Show();
            }
            else
            {
                // If it was minimized, restore it to its normal state.
                if (_currentLogWindow.WindowState == WindowState.Minimized)
                {
                    _currentLogWindow.WindowState = WindowState.Normal;
                }

                // Activate the window to bring it to the foreground.
                _currentLogWindow.Activate();

                // Re-center the log window relative to the main window every time the button is clicked.
                _currentLogWindow.Left = this.Left + (this.Width - _currentLogWindow.Width) / 2;
                _currentLogWindow.Top = this.Top + (this.Height - _currentLogWindow.Height) / 2;
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            // Filter logic
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _credentialStorageService.ClearAllCredentials();
        }

        #region Listbox Actions
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
                // SingleAccountDetails also might need _loggingService if it logs.
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

        // --- Context Menu Item Clicks ---

        // Reset password
        private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser != null)
            {
                _lastGeneratedPasswordForImmediatePrint = _passwordResetService.Reset(SelectedUser);
            }
            else
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la réinitialisation.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.Log("Password reset via context menu attempted with no user selected.", LogLevel.Warning);
            }
        }

        // Print
        private void PrintMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour l'impression.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.Log("Print via context menu attempted with no user selected.", LogLevel.Warning);
                return;
            }

            string passwordToPrint = null;
            DateTime? lastPasswordSetDate = _passwordResetService.GetLastPasswordSetDate(SelectedUser);

            if (lastPasswordSetDate.HasValue)
            {
                passwordToPrint = PasswordResetService.GenerateTempPasswordForDate(lastPasswordSetDate.Value);
            }
            else if (_lastGeneratedPasswordForImmediatePrint != null && SelectedUser == lbUsers.SelectedItem)
            {
                passwordToPrint = _lastGeneratedPasswordForImmediatePrint;
            }
            else
            {
                passwordToPrint = "DemoP@ssw0rd123!"; // Using a generic demo password here
                MessageBox.Show("Impossible de déterminer le mot de passe temporaire. " +
                                "Utilisation d'un mot de passe de démonstration pour l'aperçu.",
                                "Avertissement", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.Log($"Could not determine password for printing {SelectedUser.DisplayName}. Using demo password.", LogLevel.Warning); // Use the instance
            }

            // Now, show the print preview
            PrintService.ShowPrintPreview(SelectedUser, passwordToPrint);
            _loggingService.Log($"Print preview shown via context menu for {SelectedUser.DisplayName}. Password: {(_lastGeneratedPasswordForImmediatePrint != null ? "Generated" : "Not Reset")}", LogLevel.Info); // Use the instance
        }

        // Reset and Print
        private void ResetAndPrintMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUser != null)
            {
                _lastGeneratedPasswordForImmediatePrint = _passwordResetService.Reset(SelectedUser);

                if (_lastGeneratedPasswordForImmediatePrint != null)
                {
                    PrintService.CreatePrintDocumentDirect(SelectedUser, _lastGeneratedPasswordForImmediatePrint);
                }
                else
                {
                    MessageBox.Show("Failed to reset password for print. Check logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Aucun utilisateur sélectionné pour la réinitialisation et l'impression.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.Log("Reset and Print via context menu attempted with no user selected.", LogLevel.Warning);
            }
        }
        #endregion
    }
}