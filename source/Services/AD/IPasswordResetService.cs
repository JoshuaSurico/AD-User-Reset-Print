// File: Services.AD/IPasswordResetService.cs
using AD_User_Reset_Print.Models;
using System;

namespace AD_User_Reset_Print.Services.AD
{
    public interface IPasswordResetService
    {
        /// <summary>
        /// Resets a user's password in Active Directory and returns the generated temporary password.
        /// </summary>
        /// <param name="user">The User object for whom to reset the password.</param>
        /// <returns>The newly generated temporary password, or null if the reset failed.</returns>
        string? Reset(User user);

        /// <summary>
        /// Retrieves the LastPasswordSet date for a user from Active Directory.
        /// </summary>
        /// <param name="user">The user object to query.</param>
        /// <returns>The DateTime of the last password set, or null if not found or an error occurs.</returns>
        DateTime? GetLastPasswordSetDate(User user);
    }
}