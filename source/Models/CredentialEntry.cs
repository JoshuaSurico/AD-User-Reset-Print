using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AD_User_Reset_Print.Models
{
    public class CredentialEntry : INotifyPropertyChanged
    {
        public CredentialEntry()
        {
            _domain = string.Empty;
            _username = string.Empty;
            _password = string.Empty;
            _groups = [];
        }

        // Add a constructor that takes initial values (useful for loading existing data or modifying)
        public CredentialEntry(string domain, string username, string password, IEnumerable<string> groups)
        {
            _domain = domain;
            _username = username;
            _password = password;
            _groups = new ObservableCollection<string>(groups ?? []);
        }

        private string _domain;
        public string Domain
        {
            get => _domain;
            set
            {
                if (_domain != value)
                {
                    _domain = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _password; // Be mindful of security here!
        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _groups;
        public ObservableCollection<string> Groups
        {
            get => _groups;
            set
            {
                if (_groups != value)
                {
                    _groups = value;
                    OnPropertyChanged();
                }
            }
        }

        // Fix: Declare PropertyChanged as nullable
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // The null-conditional operator (?) safely invokes the event only if it's not null.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}