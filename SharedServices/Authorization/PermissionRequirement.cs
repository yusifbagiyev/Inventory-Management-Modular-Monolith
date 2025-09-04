using Microsoft.AspNetCore.Authorization;

namespace SharedServices.Authorization
{
    public class PermissionRequirement:IAuthorizationRequirement
    {
        public string Permission { get; }
        public PermissionRequirement(string permission)
        {
            Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        }
    }
    public class PermissionHandler:AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, 
            PermissionRequirement requirement)
        {
            // Check if user is authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Task.CompletedTask;
            }

            // Check for the permission claim - this is the key fix
            // The claim type should be "permission" (lowercase) to match what's in your JWT
            var hasPermission = context.User.Claims.Any(c =>
                c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) &&
                c.Value.Equals(requirement.Permission, StringComparison.OrdinalIgnoreCase));

            if (hasPermission)
            {
                context.Succeed(requirement);
            }

            // Also check if user is in Admin role (Admins bypass permission checks)
            else if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}