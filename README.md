# AD-User-Reset-Print

## File structure
```
.
├── AD-User-Reset-Print.csproj            # Main project file for Visual Studio build settings.
├── AD-User-Reset-Print.csproj.user       # User-specific project settings (e.g., last opened files).
├── AD-User-Reset-Print.sln               # Solution file to open the project in Visual Studio.
├── App.config                            # Application configuration settings (e.g., connection strings).
├── App.xaml                              # WPF application definition, defines global resources.
├── App.xaml.cs                           # Handles application startup logic and global events.
├── AssemblyInfo.cs                       # Project metadata (version, title, etc.).
│
├── Models/                               # Defines data structures and business objects.
│   ├── CredentialEntry.cs                # Data model for storing user credentials (domain, username, encrypted password).
│   └── User.cs                           # Data model for user information.
│
├── Properties/                           # Project-level properties and settings.
│   ├── Credentials.Designer.cs           # Auto-generated code for application settings (for Credentials.settings).
│   └── Credentials.settings              # Stores user-scoped application settings, including saved credentials.
│
├── Resources/                            # Contains static assets like images.
│   ├── Icons/                            # Various icon files used in the UI.
│   └── SplashScreen/                     # Contains assets for the application's splash screen.
│
├── Services/                             # Contains reusable logic and utility classes.
│   ├── CredentialStorageService.cs       # Manages saving and loading encrypted credentials to/from settings.
│   ├── JsonManagerService.cs             # Generic service for JSON file operations.
│   ├── LoginCheckService.cs              # Handles Active Directory login authentication and permission checks.
│   ├── PrintService.cs                   # Handles printing functionalities.
│   ├── PublipostageService.cs            # Handles create files.
│   └── SynchronizeUserService.cs         # Handles user synchronization
│
└── Views/                                # Contains all user interface windows and pages.
    ├── ADsList.xaml                      # XAML definition for the Active Directory Users list.
    ├── ADsList.xaml.cs                   # Code-behind for ADsList.xaml.
    ├── Login.xaml                        # XAML definition for the user login window.
    ├── Login.xaml.cs                     # Code-behind for Login.xaml;
    ├── MainWindow.xaml                   # XAML definition for the main application window.
    ├── MainWindow.xaml.cs                # Code-behind for MainWindow.xaml.
    ├── Settings.xaml                     # XAML definition for application settings.
    ├── Settings.xaml.cs                  # Code-behind for Settings.xaml.
    ├── SingleAccountDetails.xaml         # XAML definition for a single user account's details.
    └── SingleAccountDetails.xaml.cs      # Code-behind for SingleAccountDetails.xaml.
```

## icons
The icons are made by Pavel Kozlov, Wahyu Adam, Kirill Kazachek, Catalin Fertu, Arkinasi, Exomoon Design Studio, Freepik, Rahul Kaklotar & Febrian Hidayat from <a href="https://www.flaticon.com/fr/collections/MzMxMDkwMTA=">flaticon.com</a>