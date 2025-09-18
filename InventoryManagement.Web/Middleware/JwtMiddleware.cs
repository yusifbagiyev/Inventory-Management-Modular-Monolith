using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;

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

        public async Task InvokeAsync(HttpContext context, ITokenManager tokenRefreshService)
        {
            // Skip for static files and auth pages
            if (IsStaticFileRequest(context) || IsAuthPage(context))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                SynchronizeTokenStorage(context);
            }

            await _next(context);
        }

        private void SynchronizeTokenStorage(HttpContext context)
        {
            // If we have cookies but no session, restore to session
            if (string.IsNullOrEmpty(context.Session.GetString("JwtToken")) &&
                !string.IsNullOrEmpty(context.Request.Cookies["jwt_token"]))
            {
                context.Session.SetString("JwtToken", context.Request.Cookies["jwt_token"]!);

                var refreshToken = context.Request.Cookies["refresh_token"];
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    context.Session.SetString("RefreshToken", refreshToken);
                }

                var userData = context.Request.Cookies["user_data"];
                if (!string.IsNullOrEmpty(userData))
                {
                    context.Session.SetString("UserData", userData);
                }

                _logger.LogDebug("Synchronized tokens from cookies to session");
            }
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