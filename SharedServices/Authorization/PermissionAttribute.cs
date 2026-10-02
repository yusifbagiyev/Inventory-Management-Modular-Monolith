using Microsoft.AspNetCore.Authorization;

namespace SharedServices.Authorization
{
    /// <summary>Requires any one of the given permissions. Admins always pass.</summary>
    public class PermissionAttribute : AuthorizeAttribute
    {
        public PermissionAttribute(params string[] permissions)
        {
            Policy = string.Join(PermissionRequirement.Separator, permissions);
        }
    }
}
