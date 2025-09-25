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
                    _logger.LogWarning("No valid token for authenticated user, redirecting to login");
                    context.Response.Redirect("/Account/Login");
                    return;
                }
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