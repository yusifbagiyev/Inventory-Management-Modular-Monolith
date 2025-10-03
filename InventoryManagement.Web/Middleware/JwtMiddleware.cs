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
                using var scope=context.RequestServices.CreateScope();
                var tokenManager=scope.ServiceProvider.GetRequiredService<ITokenManager>();

                // Simply get a valid token ( will auto-refresh if needed)
                var validToken = await tokenManager.GetValidTokenAsync();

                if (string.IsNullOrEmpty(validToken))
                {
                    _logger.LogWarning("No valid token for authenticated user, clearing session and redirecting to login");

                    // Clear authentication
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Session.Clear();

                    // Clear cookies
                    context.Response.Cookies.Delete("jwt_token");
                    context.Response.Cookies.Delete("refresh_token");
                    context.Response.Cookies.Delete("user_data");

                    context.Response.Redirect("/Account/Login");
                    return;
                }
                // Store the valid token in HttpContext.Items for use by other services
                context.Items["JwtToken"] = validToken;
            }

            await _next(context);
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
                   path.Contains("/account/logout");
        }
    }
}