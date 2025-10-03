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

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip for static files and auth pages
            if (IsStaticFile(context) || IsAuthPage(context))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                using var scope = context.RequestServices.CreateScope();
                var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();

                // SECURITY FIX: Check if session has token, if not, try to restore from refresh token
                var sessionToken = context.Session.GetString("JwtToken");

                if (string.IsNullOrEmpty(sessionToken))
                {
                    _logger.LogInformation("Session token missing, attempting to restore from refresh token");

                    // Try to refresh the token using the refresh token cookie
                    var refreshed = await tokenManager.RefreshTokenAsync();

                    if (!refreshed)
                    {
                        _logger.LogWarning("Could not restore session, logging out user");
                        await SignOutUser(context);
                        context.Response.Redirect("/Account/Login");
                        return;
                    }

                    // Token was successfully refreshed and stored in session
                    sessionToken = context.Session.GetString("JwtToken");
                }

                // Get a valid token (will auto-refresh if needed)
                var validToken = await tokenManager.GetValidTokenAsync();

                if (string.IsNullOrEmpty(validToken))
                {
                    _logger.LogWarning("No valid token available, logging out user");
                    await SignOutUser(context);
                    context.Response.Redirect("/Account/Login");
                    return;
                }

                // Store the valid token in HttpContext.Items for server-side API calls
                // This is NOT exposed to the client - only used by ApiService
                context.Items["JwtToken"] = validToken;
            }

            await _next(context);
        }

        private async Task SignOutUser(HttpContext context)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();
            context.Response.Cookies.Delete("refresh_token");
        }

        private bool IsStaticFile(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            return path.Contains("/css/") || path.Contains("/js/") ||
                   path.Contains("/images/") || path.Contains("/lib/");
        }

        private bool IsAuthPage(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            return path.Contains("/account/login") ||
                   path.Contains("/account/logout") ||
                   path.Contains("/api/connection/signalr-token");
        }
    }
}