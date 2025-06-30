# AD-User-Reset-Print

## Files Structure
```
.
├── .gitignore                      # Specifies files to ignore.
├── README.md                       # Project README documentation.
└── source/                         # Main source code directory.
    ├── AD-User-Reset-Print.csproj      # Main project file for Visual Studio build settings.
    ├── AD-User-Reset-Print.sln         # Solution file to open the project in Visual Studio.
    ├── App.config                      # Application configuration settings
    ├── App.xaml                        # WPF application definition, defines global resources.
    ├── App.xaml.cs                     # Handles application startup logic and global events.
    ├── AssemblyInfo.cs                 # Project metadata (version, title, etc.).
    ├── Models/                         # Defines data structures and business objects.
    │   ├── AppSettings.cs                  # Data model for application settings.
    │   ├── CredentialEntry.cs              # Data model for storing user credentials
    │   ├── ProgressReport.cs               # Data model for progress reporting during operations.
    │   └── User.cs                         # Data model for AD user information.
    ├── Properties/                     # Project-level properties and settings.
    │   ├── Credentials.Designer.cs         # Auto-generated code for application settings (for Credentials.settings).
    │   └── Credentials.settings            # Stores user-scoped application settings, including saved credentials.
    ├── Resources/                      # Contains static assets like images.
    │   ├── Icons/                          # Various icon files used in the UI.
    │   └── SplashScreen/                   # Contains assets for the application's splash screen.
    ├── Services/                       # Contains reusable logic and utility classes.
    │   ├── AD/                              # Services related to Active Directory operations.
    │   │   ├── ADSourceCheckService.cs         # Service to check Active Directory source availability.
    │   │   ├── PasswordResetService.cs         # Service for Active Directory password reset operations.
    │   │   └── SynchronizeUserService.cs       # Handles user synchronization with Active Directory.
    │   ├── CredentialStorageService.cs     # Manages saving and loading encrypted credentials to/from settings.
    │   ├── JsonManagerService.cs           # Generic service for JSON file operations.
    │   ├── LoggingService.cs               # Service for application logging.
    │   └── PrintService.cs                 # Handles printing functionalities.
    └── Views/                          # Contains all user interface windows and pages.
        ├── ADSourceConfigWindow.xaml       # XAML definition for Active Directory source configuration window.
        ├── ADSourceConfigWindow.xaml.cs    # Code-behind for ADSourceConfigWindow.xaml.
        ├── ADSourcesWindow.xaml            # XAML definition for managing Active Directory sources.
        ├── ADSourcesWindow.xaml.cs         # Code-behind for ADSourcesWindow.xaml.
        ├── CustomMessageBox.xaml           # XAML definition for custom message box dialogs.
        ├── CustomMessageBox.xaml.cs        # Code-behind for CustomMessageBox.xaml.
        ├── LogsWindow.xaml                 # XAML definition for displaying application logs.
        ├── LogsWindow.xaml.cs              # Code-behind for LogsWindow.xaml.
        ├── MainWindow.xaml                 # XAML definition for the main application window.
        ├── MainWindow.xaml.cs              # Code-behind for MainWindow.xaml.
        ├── PrintPreviewWindow.xaml         # XAML definition for print preview window.
        ├── PrintPreviewWindow.xaml.cs      # Code-behind for PrintPreviewWindow.xaml.
        ├── Settings.xaml                   # XAML definition for application settings.
        ├── Settings.xaml.cs                # Code-behind for Settings.xaml.
        ├── SingleAccountDetails.xaml       # XAML definition for a single user account's details.
        └── SingleAccountDetails.xaml.cs    # Code-behind for SingleAccountDetails.xaml.
```

## Icons
The icons are made by Pavel Kozlov, Wahyu Adam, Kirill Kazachek, Catalin Fertu, Arkinasi, Exomoon Design Studio, Freepik, Rahul Kaklotar & Febrian Hidayat from <a href="https://www.flaticon.com/fr/collections/MzMxMDkwMTA=">flaticon.com</a>
