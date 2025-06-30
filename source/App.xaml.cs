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

            // 1. Always open the MainWindow first
            MainWindow mainWindow = new();
            mainWindow.Show();

            // 2. Check if any credentials are saved using the static method
            /*while (!CredentialStorageService.AreCredentialsSaved())
            {
                // 3. If no credentials, open ADSourcesWindow on top as a modal dialog
                ADSourcesWindow adSources = new()
                {
                    Owner = mainWindow
                };

                bool? result = adSources.ShowDialog();

                // If result is true, it means credentials were saved, so the loop will exit on the next check.
                // If result is false or null, it means the user closed the ADSourcesWindow without saving.
                if (result == false || result == null) // User cancelled or closed the config window
                {
                    CustomMessageBox customMsgBox = new(
                        "Aucune information d'identification AD n'a été configurée. L'application nécessite des informations d'identification pour fonctionner correctement.\n\n" +
                        "Voulez-vous les configurer maintenant ? (Cliquez sur 'Non' pour quitter l'application)",
                        "Configuration Requise",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    )
                    { 
                        Owner = mainWindow 
                    };

                    bool? customDialogResult = customMsgBox.ShowDialog(); // Show your custom dialog

                    if (customDialogResult == true) // User clicked 'Oui' (Yes)
                    {
                        // Loop will repeat
                    }
                    else // User clicked 'Non' (No) or closed the custom dialog
                    {
                        Application.Current.Shutdown();
                        return;
                    }
                }
            }*/
        }
    }
}