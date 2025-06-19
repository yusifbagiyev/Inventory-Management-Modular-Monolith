using Microsoft.AspNetCore.Authorization;

namespace IdentityService.Shared.Authorization
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
            var hasPermission=context.User.Claims
                .Any(c => c.Type == "permission" && c.Value == requirement.Permission);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}