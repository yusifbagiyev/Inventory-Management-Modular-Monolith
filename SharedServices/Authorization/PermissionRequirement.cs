using Microsoft.AspNetCore.Authorization;

namespace SharedServices.Authorization
{
    /// <summary>Met when the user is an Admin or holds any one of the permissions.</summary>
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public const string Separator = "|";

        public IReadOnlyList<string> Permissions { get; }

        public PermissionRequirement(string policyName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
            Permissions = policyName.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return Task.CompletedTask;

            if (context.User.IsInRole("Admin") || context.User.Claims.Any(c =>
                    c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) &&
                    requirement.Permissions.Contains(c.Value, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
