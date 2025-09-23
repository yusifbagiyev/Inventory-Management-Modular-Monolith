using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(
            RequestDelegate next,
            ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenManager tokenManager)
        {
            // Skip for static files and auth pages
            if (IsStaticFileRequest(context) || IsAuthPage(context))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                await SynchronizeAndValidateTokens(context, tokenManager);
            }

            await _next(context);
        }

        private async Task SynchronizeAndValidateTokens(HttpContext context, ITokenManager tokenManager)
        {
            var sessionToken = context.Session.GetString("JwtToken");
            var cookieToken = context.Request.Cookies["jwt_token"];

            // If session is empty but cookie exists, restore from cookie
            if (string.IsNullOrEmpty(sessionToken) && !string.IsNullOrEmpty(cookieToken))
            {
                _logger.LogInformation("Restoring session from cookies after restart");

                // Validate the cookie token is still valid
                try
                {
                    var refreshToken = context.Request.Cookies["refresh_token"];
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        // Try to refresh the token to ensure it's valid
                        var validToken = await tokenManager.GetValidTokenAsync();
                        if (!string.IsNullOrEmpty(validToken))
                        {
                            // Token is valid or successfully refreshed
                            context.Session.SetString("JwtToken", validToken);
                            context.Session.SetString("RefreshToken", refreshToken);

                            var userData = context.Request.Cookies["user_data"];
                            if (!string.IsNullOrEmpty(userData))
                            {
                                context.Session.SetString("UserData", userData);
                            }

                            _logger.LogInformation("Successfully restored and validated tokens from cookies");
                        }
                        else
                        {
                            // Token is invalid, force re-login
                            await ClearAuthenticationAsync(context);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore tokens from cookies");
                    await ClearAuthenticationAsync(context);
                }
            }
            // If session exists, validate it's still valid
            else if (!string.IsNullOrEmpty(sessionToken))
            {
                var validToken = await tokenManager.GetValidTokenAsync();
                if (string.IsNullOrEmpty(validToken))
                {
                    await ClearAuthenticationAsync(context);
                }
            }
        }

        private async Task ClearAuthenticationAsync(HttpContext context)
        {
            context.Session.Clear();
            context.Response.Cookies.Delete("jwt_token");
            context.Response.Cookies.Delete("refresh_token");
            context.Response.Cookies.Delete("user_data");

            // Sign out from cookie authentication
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        private bool IsStaticFileRequest(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var staticExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg",
                                          ".gif", ".ico", ".woff", ".woff2", ".ttf", ".svg" };
            return staticExtensions.Any(ext => path.EndsWith(ext));
        }
        private bool IsAuthPage(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments("/Account/Login") ||
                   context.Request.Path.StartsWithSegments("/Account/Logout") ||
                   context.Request.Path.StartsWithSegments("/Account/AccessDenied");
        }

    }
}