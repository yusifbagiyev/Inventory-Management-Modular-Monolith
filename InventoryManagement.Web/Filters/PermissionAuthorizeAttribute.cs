using InventoryManagement.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryManagement.Web.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method ,AllowMultiple =true)]
    public class PermissionAuthorizeAttribute:Attribute,IAuthorizationFilter
    {
        private readonly string _permission;
        private readonly string _alternatePermission;

        public PermissionAuthorizeAttribute(string permission)
        {
            _permission= permission;
            _alternatePermission = null;
        }

        public PermissionAuthorizeAttribute(string permission,string alternatePermission)
        {
            _permission = permission;
            _alternatePermission = alternatePermission;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // First check if user is authenticated at all
            var user = context.HttpContext.User;

            if(user == null || !user?.Identity?.IsAuthenticated == true)
            {
                // Not authenticated -redirect to login
                context.Result = new RedirectToActionResult("Login", "Account", new
                {
                    returnUrl = context.HttpContext.Request.Path
                });
                return;
            }

            if(user!= null)
            {
                // Check if user has the required permission (or alternate permission)
                bool hasPermission = user.HasPermission(_permission);

                if (!hasPermission && !string.IsNullOrEmpty(_alternatePermission))
                {
                    hasPermission=user.HasPermission(_alternatePermission);
                }

                if (!hasPermission)
                {
                    // User is authenticated but lacks permission - show access denied
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                    return;
                }
                // User has permission - allow the request to proceed
            }
        }
    }
}