using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;

        // Paths that don't require authentication
        private readonly HashSet<string> _excludedPaths = new()
        {
            "/account/login",
            "/account/logout",
            "/home/privacy",
            "/health",
            "/error"
        };

        public JwtMiddleware(
            RequestDelegate next,
            ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip authentication for excluded paths and static files
            if (ShouldSkipAuthentication(context))
            {
                await _next(context);
                return;
            }

            // Check if user is authenticated
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                using var scope = context.RequestServices.CreateScope();
                var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();

                try
                {
                    // Validate and refresh token if needed
                    var validToken = await tokenManager.GetValidTokenAsync();

                    if (string.IsNullOrEmpty(validToken))
                    {
                        _logger.LogWarning("No valid token available for authenticated user at {Path}",
                            context.Request.Path);

                        // Clear authentication and redirect to login
                        await ClearAuthenticationAsync(context);

                        // For AJAX requests, return 401
                        if (IsAjaxRequest(context))
                        {
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("Authentication required");
                            return;
                        }

                        // For regular requests, redirect to login
                        context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(context.Request.Path)}");
                        return;
                    }

                    // Store valid token in context for downstream use
                    // This is safe as HttpContext.Items is per-request
                    context.Items["JwtToken"] = validToken;

                    // Add security headers
                    AddSecurityHeaders(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing JWT token for {Path}", context.Request.Path);

                    // Don't block the request on error, let it proceed
                    // The API calls will fail with 401 if token is invalid
                }
            }

            await _next(context);
        }



        private bool ShouldSkipAuthentication(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Skip for static files
            if (IsStaticFile(path))
                return true;

            // Skip for excluded paths
            if (_excludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Skip for health checks
            if (path.Contains("/health"))
                return true;

            return false;
        }



        private bool IsStaticFile(string path)
        {
            var staticExtensions = new[] { ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".eot" };
            var staticPaths = new[] { "/css/", "/js/", "/images/", "/lib/", "/fonts/" };

            return staticPaths.Any(sp => path.Contains(sp, StringComparison.OrdinalIgnoreCase)) ||
                   staticExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }



        private bool IsAjaxRequest(HttpContext context)
        {
            return context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                   context.Request.Headers.Accept.ToString().Contains("application/json");
        }



        private async Task ClearAuthenticationAsync(HttpContext context)
        {
            try
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Session.Clear();

                // Clear auth cookies
                var authCookies = new[] { "token_version", ".AspNetCore.Session" };
                foreach (var cookie in authCookies)
                {
                    if (context.Request.Cookies.ContainsKey(cookie))
                    {
                        context.Response.Cookies.Delete(cookie);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing authentication state");
            }
        }


        private void AddSecurityHeaders(HttpContext context)
        {
            // Prevent token leakage in referrer
            if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
            {
                context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            // Prevent caching of authenticated pages
            if (!IsStaticFile(context.Request.Path))
            {
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
            }
        }
    }
}