using InventoryManagement.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryManagement.Web.Filters
{
    /// <summary>Passes if the user holds any of the permissions, and several attributes on one action must all pass.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class PermissionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _permissions;

        public PermissionAuthorizeAttribute(params string[] permissions)
        {
            _permissions = permissions;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            if (_permissions.Any(user.HasPermission))
                return;

            var isAjax = context.HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest";
            context.Result = isAjax
                ? new ObjectResult(new { isSuccess = false, success = false, message = "Access denied" }) { StatusCode = StatusCodes.Status403Forbidden }
                : new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
