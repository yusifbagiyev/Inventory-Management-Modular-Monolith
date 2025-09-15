using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenRefreshService tokenRefreshService)
        {
            try
            {
                // Skip for static files and auth endpoints
                if (ShouldSkipTokenManagement(context))
                {
                    await _next(context);
                    return;
                }

                // Only process for authenticated users
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    // Ensure tokens are synchronized across storage
                    await EnsureTokenSynchronization(context, tokenRefreshService);
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT middleware");
                await _next(context);
            }
        }

        private async Task EnsureTokenSynchronization(HttpContext context, ITokenRefreshService tokenRefreshService)
        {
            // Check if we have valid tokens in session
            var accessToken = context.Session.GetString("JwtToken");
            var refreshToken = context.Session.GetString("RefreshToken");

            // If session is empty but user is authenticated via cookie
            if (string.IsNullOrEmpty(accessToken) && context.User.Identity.IsAuthenticated)
            {
                // Check cookies for tokens
                accessToken = context.Request.Cookies["jwt_token"];
                refreshToken = context.Request.Cookies["refresh_token"];

                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    // Restore to session
                    context.Session.SetString("JwtToken", accessToken);
                    context.Session.SetString("RefreshToken", refreshToken);

                    // Also restore user data if available
                    var userData = context.Request.Cookies["user_data"];
                    if (!string.IsNullOrEmpty(userData))
                    {
                        context.Session.SetString("UserData", userData);
                    }
                }
                else
                {
                    // No tokens available, force re-authentication
                    _logger.LogWarning("User authenticated but no tokens available, forcing re-authentication");
                    await ForceReauthentication(context);
                    return;
                }
            }

            // Check if token needs refresh
            if (!string.IsNullOrEmpty(accessToken) && tokenRefreshService.IsTokenExpiringSoon(accessToken))
            {
                _logger.LogInformation("Token expiring soon, attempting refresh");

                var newTokens = await tokenRefreshService.RefreshTokenIfNeededAsync();
                if (newTokens == null)
                {
                    _logger.LogWarning("Token refresh failed, forcing re-authentication");
                    await ForceReauthentication(context);
                }
            }
        }

        private async Task ForceReauthentication(HttpContext context)
        {
            // Clear all authentication data
            context.Session.Clear();

            // Clear cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddDays(-1)
            };

            context.Response.Cookies.Delete("jwt_token", cookieOptions);
            context.Response.Cookies.Delete("refresh_token", cookieOptions);
            context.Response.Cookies.Delete("user_data", cookieOptions);

            // Sign out from cookie authentication
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirect to login
            context.Response.Redirect("/Account/Login");
        }

        private bool ShouldSkipTokenManagement(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // Skip static files
            if (path.Contains("/css/") || path.Contains("/js/") || path.Contains("/lib/") ||
                path.Contains("/images/") || path.Contains(".ico"))
                return true;

            // Skip auth endpoints
            if (path.StartsWith("/account/login") ||
                path.StartsWith("/account/logout") ||
                path.StartsWith("/account/accessdenied"))
                return true;

            return false;
        }
    }
}