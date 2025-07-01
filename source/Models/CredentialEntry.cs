using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace AD_User_Reset_Print.Models
{
    public class CredentialEntry : INotifyPropertyChanged, IDisposable
    {
        // Private backing fields are necessary for INotifyPropertyChanged
        private string _domain;
        private string _username;
        private ObservableCollection<string> _groups;
        private byte[] _encryptedPasswordBytes;
        private SecureString _password;
        private bool _disposedValue;

        // --- PROPERTIES with PropertyChanged Notification ---

        public string Domain
        {
            get => _domain;
            set { if (_domain != value) { _domain = value; OnPropertyChanged(); } }
        }

        public string Username
        {
            get => _username;
            set { if (_username != value) { _username = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<string> Groups
        {
            get => _groups;
            set { if (_groups != value) { _groups = value; OnPropertyChanged(); } }
        }

        // This is the property that will be serialized to JSON.
        public byte[] EncryptedPasswordBytes
        {
            get => _encryptedPasswordBytes;
            set { if (_encryptedPasswordBytes != value) { _encryptedPasswordBytes = value; OnPropertyChanged(); } }
        }

        // This is the property we use in our code. It's ignored by JSON.
        [JsonIgnore]
        public SecureString Password
        {
            get
            {
                if ((_password == null || _password.Length == 0) && EncryptedPasswordBytes?.Length > 0)
                {
                    LoadPasswordFromEncryptedBytes();
                }
                return _password;
            }
            set
            {
                if (_password != value)
                {
                    _password?.Dispose();
                    _password = value;
                    EncryptPasswordForStorage();
                    OnPropertyChanged();
                }
            }
        }

        // --- CONSTRUCTORS ---

        [JsonConstructor]
        public CredentialEntry(string domain, string username, ObservableCollection<string> groups, byte[] encryptedPasswordBytes)
        {
            _domain = domain;
            _username = username;
            _groups = groups ?? [];
            _encryptedPasswordBytes = encryptedPasswordBytes;
            _password = new SecureString(); // Initialize as empty, to be loaded lazily.
        }

        public CredentialEntry(string domain, string username, string password, IEnumerable<string> groups)
        {
            _domain = domain;
            _username = username;
            _groups = new ObservableCollection<string>(groups ?? []);
            _password = new SecureString();

            if (!string.IsNullOrEmpty(password))
            {
                foreach (char c in password) { _password.AppendChar(c); }
            }
            _password.MakeReadOnly();
            EncryptPasswordForStorage();
        }

        // --- ENCRYPTION / DECRYPTION LOGIC ---

        private void LoadPasswordFromEncryptedBytes()
        {
            try
            {
                byte[] decryptedBytes = ProtectedData.Unprotect(EncryptedPasswordBytes, null, DataProtectionScope.CurrentUser);
                string decryptedString = Encoding.UTF8.GetString(decryptedBytes);

                var newSecureString = new SecureString();
                foreach (char c in decryptedString) { newSecureString.AppendChar(c); }
                newSecureString.MakeReadOnly();

                _password?.Dispose();
                _password = newSecureString;
                Array.Clear(decryptedBytes, 0, decryptedBytes.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to decrypt password: {ex.Message}");
                _password?.Dispose();
                _password = new SecureString();
            }
        }

        private void EncryptPasswordForStorage()
        {
            if (_password == null || _password.Length == 0)
            {
                EncryptedPasswordBytes = null;
                return;
            }

            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToBSTR(_password);
                string plainText = Marshal.PtrToStringBSTR(valuePtr);
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                EncryptedPasswordBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                Array.Clear(plainBytes, 0, plainBytes.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to encrypt password: {ex.Message}");
                EncryptedPasswordBytes = null;
            }
            finally
            {
                if (valuePtr != IntPtr.Zero) Marshal.ZeroFreeBSTR(valuePtr);
            }
        }

        // --- INotifyPropertyChanged & IDisposable Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _password?.Dispose();
                _disposedValue = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}