// File: Services.AD/ISynchronizeUserService.cs
using AD_User_Reset_Print.Models; // Assuming User and ProgressReport models are here

namespace AD_User_Reset_Print.Services.AD
{
    public interface ISynchronizeUserService
    {
        /// <summary>
        /// Synchronizes user data from Active Directory groups based on configured credentials.
        /// </summary>
        /// <param name="progress">An object to report synchronization progress.</param>
        /// <returns>A list of synchronized User objects.</returns>
        Task<List<User>> Sync(IProgress<ProgressReport> progress);
    }
}