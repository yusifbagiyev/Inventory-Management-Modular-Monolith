using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.IdentityModel.Tokens.Jwt;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;
        private const int MAX_INACTIVE_MINUTES = 60; // Force logout after 1 hour of inactivity

        public JwtMiddleware(
            RequestDelegate next,
            ILogger<JwtMiddleware> logger)
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

                // Check if session has a token
                var currentToken = context.Session.GetString("JwtToken");

                // CRITICAL FIX: If user is authenticated but session is empty,
                // this means they have a valid auth cookie but session was lost
                // (server restart, session timeout, etc.)
                if (string.IsNullOrEmpty(currentToken))
                {
                    _logger.LogInformation("User authenticated but no token in session - attempting to restore from refresh token");

                    // Try to refresh the token to restore the session
                    var refreshSuccess = await tokenManager.RefreshTokenAsync();

                    if (refreshSuccess)
                    {
                        // Session now has the new token, get it
                        currentToken = context.Session.GetString("JwtToken");
                        _logger.LogInformation("Successfully restored session from refresh token");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to restore session - refresh token may be expired");
                        await ClearAuthenticationAndRedirect(context);
                        return;
                    }
                }

                // Now validate the token we have
                if (!string.IsNullOrEmpty(currentToken))
                {
                    // Check if token needs refresh (expiring soon)
                    if (IsTokenExpiredOrExpiring(currentToken))
                    {
                        _logger.LogInformation("Token expiring soon, refreshing...");
                        var refreshSuccess = await tokenManager.RefreshTokenAsync();

                        if (refreshSuccess)
                        {
                            currentToken = context.Session.GetString("JwtToken");
                            _logger.LogInformation("Token refreshed successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Token refresh failed");
                            await ClearAuthenticationAndRedirect(context);
                            return;
                        }
                    }

                    // Store valid token in HttpContext.Items for use by ApiService
                    context.Items["JwtToken"] = currentToken;

                    // Update last activity time
                    context.Session.SetString("LastActivity", DateTime.Now.ToString("o"));
                }
                else
                {
                    // No token available even after refresh attempt
                    _logger.LogWarning("No valid token available, clearing authentication");
                    await ClearAuthenticationAndRedirect(context);
                    return;
                }
            }

            await _next(context);
        }

        private bool IsTokenExpiredOrExpiring(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var expiryTime = jwtToken.ValidTo.ToLocalTime();
                var now = DateTime.Now;

                // Refresh if less than 5 minutes remaining
                var bufferTime = TimeSpan.FromMinutes(5);
                var expiresIn = expiryTime - now;

                return expiresIn <= bufferTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JWT token");
                return true; // Treat parse errors as expired
            }
        }

        private async Task ClearAuthenticationAndRedirect(HttpContext context)
        {
            // Clear all authentication state
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();

            context.Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            context.Response.Redirect("/Account/Login");
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
                   path.Contains("/account/refreshtoken");
        }
    }
}