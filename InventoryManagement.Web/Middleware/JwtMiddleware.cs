using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;
        private static readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
        private static readonly Dictionary<string, DateTime> _refreshAttempts = new();
        private static readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(30);

        public JwtMiddleware(
            RequestDelegate next, 
            ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITokenRefreshService tokenRefreshService)
        {
            try
            {
                // Skip for static files, auth pages, and AJAX token refresh requests
                if (ShouldSkipTokenRefresh(context))
                {
                    await _next(context);
                    return;
                }

                // Only process authenticated users
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    // Get the current token
                    var token = GetTokenFromContext(context);

                    if (!string.IsNullOrEmpty(token))
                    {
                        // Check if we should attempt refresh
                        if (await ShouldRefreshToken(token, context, tokenRefreshService))
                        {
                            await AttemptTokenRefresh(context, tokenRefreshService);
                        }
                    }
                    else if (HasAuthenticationCookie(context))
                    {
                        // User has auth cookie but no JWT token - likely session expired
                        _logger.LogWarning("User authenticated via cookie but missing JWT token");

                        // Try to refresh using refresh token if available
                        var refreshToken = GetRefreshTokenFromContext(context);
                        if (!string.IsNullOrEmpty(refreshToken))
                        {
                            await AttemptTokenRefresh(context, tokenRefreshService);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT middleware");
                // Don't throw - let the request continue
            }

            await _next(context);
        }

        private bool ShouldSkipTokenRefresh(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // Skip static files
            if (IsStaticFile(path))
                return true;

            // Skip auth endpoints
            if (path.StartsWith("/account/login") ||
                path.StartsWith("/account/logout") ||
                path.StartsWith("/account/refreshtoken") ||
                path.StartsWith("/account/accessdenied"))
                return true;

            // Skip AJAX refresh requests to prevent loops
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" &&
                path.Contains("refresh"))
                return true;

            return false;
        }

        private bool IsStaticFile(string path)
        {
            var staticExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg",
                                          ".gif", ".ico", ".woff", ".woff2", ".ttf",
                                          ".svg", ".map" };
            return staticExtensions.Any(ext => path.EndsWith(ext));
        }

        private bool HasAuthenticationCookie(HttpContext context)
        {
            return context.Request.Cookies.ContainsKey(".AspNetCore.Cookies") ||
                   context.Request.Cookies.ContainsKey("jwt_token");
        }

        private string? GetTokenFromContext(HttpContext context)
        {
            // Check in order of preference
            return context.Items["JwtToken"] as string ??
                   context.Session.GetString("JwtToken") ??
                   context.Request.Cookies["jwt_token"];
        }

        private string? GetRefreshTokenFromContext(HttpContext context)
        {
            return context.Items["RefreshToken"] as string ??
                   context.Session.GetString("RefreshToken") ??
                   context.Request.Cookies["refresh_token"];
        }

        private async Task<bool> ShouldRefreshToken(string token, HttpContext context, ITokenRefreshService tokenRefreshService)
        {
            // Check if token is expiring soon
            if (!tokenRefreshService.IsTokenExpiringSoon(token))
                return false;

            // Check cooldown to prevent rapid refresh attempts
            var userKey = context.User.Identity?.Name ?? "anonymous";
            lock (_refreshAttempts)
            {
                if (_refreshAttempts.TryGetValue(userKey, out var lastAttempt))
                {
                    if (DateTime.Now - lastAttempt < _refreshCooldown)
                    {
                        _logger.LogDebug("Skipping refresh due to cooldown for user {User}", userKey);
                        return false;
                    }
                }
            }

            return true;
        }

        private async Task AttemptTokenRefresh(HttpContext context, ITokenRefreshService tokenRefreshService)
        {
            var userKey = context.User.Identity?.Name ?? "anonymous";

            // Acquire semaphore with timeout
            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Token refresh already in progress");
                return;
            }

            try
            {
                // Update last attempt time
                lock (_refreshAttempts)
                {
                    _refreshAttempts[userKey] = DateTime.UtcNow;

                    // Clean up old entries
                    var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
                    var oldKeys = _refreshAttempts
                        .Where(kvp => kvp.Value < cutoff)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in oldKeys)
                    {
                        _refreshAttempts.Remove(key);
                    }
                }

                _logger.LogInformation("Attempting token refresh for user {User}", userKey);

                var result = await tokenRefreshService.RefreshTokenIfNeededAsync();

                if (result != null)
                {
                    _logger.LogInformation("Token refreshed successfully for user {User}", userKey);

                    // Update tokens in context for immediate use
                    context.Items["JwtToken"] = result.AccessToken;
                    context.Items["RefreshToken"] = result.RefreshToken;
                }
                else
                {
                    _logger.LogWarning("Token refresh failed for user {User}", userKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token for user {User}", userKey);
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }
    }
}