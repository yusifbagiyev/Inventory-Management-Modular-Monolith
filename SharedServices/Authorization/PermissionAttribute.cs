using Microsoft.AspNetCore.Authorization;

namespace SharedServices.Authorization
{
    public class PermissionAttribute:AuthorizeAttribute
    {
        public PermissionAttribute(string permission)
        {
            Policy = permission;
        }
    }
}