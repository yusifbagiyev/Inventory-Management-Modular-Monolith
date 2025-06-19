using Microsoft.AspNetCore.Authorization;

namespace IdentityService.Shared.Authorization
{
    public class PermissionAttribute:AuthorizeAttribute
    {
        public PermissionAttribute(string permission)
        {
            Policy = permission;
        }
    }
}