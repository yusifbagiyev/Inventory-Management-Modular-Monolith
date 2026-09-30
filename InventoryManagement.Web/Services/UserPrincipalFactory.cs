using System.Globalization;
using System.Security.Claims;
using IdentityService.Application.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Services
{
    /// <summary>
    /// Builds the auth-cookie principal from the identity module, and keeps it current: roles and
    /// permissions are re-read periodically so an admin's change (or deactivation) takes effect
    /// without the user having to sign out.
    /// </summary>
    public static class UserPrincipalFactory
    {
        public const string PermissionClaim = "permission";
        private const string RefreshedAtClaim = "ClaimsRefreshedAt";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        public static ClaimsPrincipal Create(UserDto user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new("FirstName", user.FirstName),
                new("LastName", user.LastName),
                new(RefreshedAtClaim, DateTimeOffset.UtcNow.ToString("o"))
            };
            claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(user.Permissions.Select(p => new Claim(PermissionClaim, p)));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        /// <summary>Cookie OnValidatePrincipal hook.</summary>
        public static async Task RefreshAsync(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            if (principal == null) return;

            var refreshedAt = principal.FindFirst(RefreshedAtClaim)?.Value;
            if (DateTimeOffset.TryParse(refreshedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                && DateTimeOffset.UtcNow - at < RefreshInterval)
                return;

            if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                context.RejectPrincipal();
                return;
            }

            var auth = context.HttpContext.RequestServices.GetRequiredService<IdentityAuth>();
            var user = await auth.GetUserAsync(userId);
            if (user == null || !user.IsActive)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            context.ReplacePrincipal(Create(user));
            context.ShouldRenew = true;
        }
    }
}
