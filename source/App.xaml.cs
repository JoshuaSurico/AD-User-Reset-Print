using AD_User_Reset_Print.Views;
using AD_User_Reset_Print.Services;
using System.Configuration;
using System.Data;
using System.Windows;

namespace AD_User_Reset_Print
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {

        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if any credentials are saved using the static method
            if (CredentialStorageService.AreCredentialsSaved())
            {
                // If credentials exist, directly open the main application window
                MainWindow mainWindow = new();
                mainWindow.Show();
            }
            else
            {
                // If no credentials are saved, open the TargetedADsList window to allow the user to add credentials.
                TargetedADsList targetedADsListWindow = new();
                targetedADsListWindow.Show();
            }
        }
    }
}