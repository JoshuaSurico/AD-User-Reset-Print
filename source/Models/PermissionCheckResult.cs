// File: Models/PermissionCheckResult.cs
namespace AD_User_Reset_Print.Models
{
    public class PermissionCheckResult
    {
        public bool IsSuccessful { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool HasFullPermission { get; set; } = false;
        public bool IsHighlyPrivileged { get; set; } = false;
        public Dictionary<string, bool> TargetGroupPermissions { get; set; } = [];
    }
}