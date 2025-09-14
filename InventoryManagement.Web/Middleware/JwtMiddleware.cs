using InventoryManagement.Web.Services.Interfaces;

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

        public async Task InvokeAsync(HttpContext context, ITokenRefreshService tokenRefreshService)
        {
            // Skip for static files and auth pages
            if (IsStaticFileRequest(context) || IsAuthPage(context))
            {
                await _next(context);
                return;
            }

            // Only check authenticated users
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    await tokenRefreshService.RefreshTokenIfNeededAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in JWT middleware token refresh");
                }
            }

            await _next(context);
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