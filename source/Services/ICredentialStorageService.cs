// File: Services/ICredentialStorageService.cs
using AD_User_Reset_Print.Models;

namespace AD_User_Reset_Print.Services
{
    public interface ICredentialStorageService
    {
        List<CredentialEntry> LoadCredentials();
        void SaveCredentials(List<CredentialEntry> credentialsToSave);
        void ClearAllCredentials();
        bool AreCredentialsSaved();
    }
}