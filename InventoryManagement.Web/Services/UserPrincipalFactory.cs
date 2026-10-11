using System.Globalization;
using System.Security.Claims;
using IdentityService.Application.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Services
{
    /// <summary>Builds the cookie principal and re-reads it every five minutes so role changes apply without a re-login.</summary>
    public static class UserPrincipalFactory
    {
        public const string PermissionClaim = "permission";
        private const string RefreshedAtClaim = "ClaimsRefreshedAt";
        private const string SessionStampClaim = InventoryManagement.Web.Extensions.AuthenticationExtensions.SessionStampClaim;
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        public static ClaimsPrincipal Create(UserDto user, string? sessionStamp)
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
            if (sessionStamp != null)
                claims.Add(new Claim(SessionStampClaim, sessionStamp));
            claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(user.Permissions.Select(p => new Claim(PermissionClaim, p)));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        /// <summary>Cookie OnValidatePrincipal hook, which also ends the session on a stamp change.</summary>
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
            var stamp = user is { IsActive: true } ? await auth.GetSessionStampAsync(userId) : null;
            var held = principal.FindFirst(SessionStampClaim)?.Value;
            // A session from before the stamp claim existed takes the current stamp once
            if (user == null || !user.IsActive || stamp == null
                || (held != null && held != stamp))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            context.ReplacePrincipal(Create(user, stamp));
            // The session check is still validated above, but only the user's own requests extend the session
            context.ShouldRenew = !InventoryManagement.Web.Extensions.AuthenticationExtensions.IsSessionCheck(context.Request);
        }
    }
}
