// File: Services.AD/IADSourceCheckService.cs
using AD_User_Reset_Print.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AD_User_Reset_Print.Services.AD
{
    public interface IADSourceCheckService
    {
        event Action<string> OnOutputMessage;
        Task<PermissionCheckResult> RunPermissionCheckAsync(string domain, string username, System.Security.SecureString password, List<string> targetGroups);
    }
}