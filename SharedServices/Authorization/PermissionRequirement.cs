using Microsoft.AspNetCore.Authorization;

namespace SharedServices.Authorization
{
    /// <summary>Met when the user holds any one of <see cref="Permissions"/>, or is an Admin.</summary>
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

            // Admins bypass permission checks; everyone else needs a "permission" claim.
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
