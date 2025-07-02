using AD_User_Reset_Print.Services;
using AD_User_Reset_Print.Services.AD;
using AD_User_Reset_Print.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace AD_User_Reset_Print
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            // Constructor is fine being empty here, as OnStartup handles the DI setup.
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            var logger = ServiceProvider.GetRequiredService<ILoggingService>();
            logger.Log("Application startup initiated.");

            var credentialStorageService = ServiceProvider.GetRequiredService<ICredentialStorageService>();

            // Always open the MainWindow first.
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            logger.Log("MainWindow displayed.");

            // Initial check for credentials at startup.
            // This now relies on the ADSourcesWindow's DialogResult and its internal logic.
            if (!credentialStorageService.AreCredentialsSaved())
            {
                logger.Log("No AD credentials found on initial startup. Opening ADSourcesWindow for configuration.", LogLevel.Warning);

                // Call the helper method to prompt for credentials
                // This helper method will handle the loop and shutdown if necessary.
                PromptForCredentials(mainWindow, logger, credentialStorageService);
            }

            logger.Log("Application startup complete. Credentials are configured (or user chose to exit).", LogLevel.Info);
        }

        /// <summary>
        /// Prompts the user to configure credentials if none are saved.
        /// This method is called repeatedly until credentials are saved or the user chooses to exit.
        /// It relies on ADSourcesWindow's internal Closing event logic.
        /// </summary>
        private void PromptForCredentials(Window owner, ILoggingService logger, ICredentialStorageService credentialStorageService)
        {
            while (!credentialStorageService.AreCredentialsSaved())
            {
                var adSources = ServiceProvider.GetRequiredService<ADSourcesWindow>();
                adSources.Owner = owner;

                // ShowDialog will return null if the window is closed via the 'X' button or system menu,
                // or true/false based on DialogResult set internally.
                bool? result = adSources.ShowDialog();

                // If result is false (user chose to exit from ADSourcesWindow's prompt)
                if (result == false)
                {
                    logger.Log("User chose to exit application from ADSourcesWindow due to unconfigured AD credentials.", LogLevel.Info);
                    Application.Current.Shutdown();
                    return; // Exit this method and stop further startup
                }
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Core Services (usually Singletons if they manage app-wide state or resources)
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IJsonManagerService, JsonManagerService>();
            services.AddSingleton<ICredentialStorageService, CredentialStorageService>();
            services.AddSingleton<IPasswordResetService, PasswordResetService>();
            services.AddTransient<ISynchronizeUserService, SynchronizeUserService>();
            services.AddTransient<IADSourceCheckService, ADSourceCheckService>();

            // UI Windows (usually Transient, as new instances are created per request)
            services.AddSingleton<MainWindow>();
            services.AddTransient<LogsWindow>();
            services.AddTransient<ADSourcesWindow>();
            services.AddTransient<ADSourceConfigWindow>();
        }
    }
}