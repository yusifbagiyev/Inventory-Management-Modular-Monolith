using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json;

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

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            try
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var userId = context.User.Identity.Name ?? "Unknown";
                    var requestPath = context.Request.Path.Value ?? "Unknown";

                    string? token = null;
                    string? refreshToken = null;

                    // Try to get tokens from session first (always available for logged-in users)
                    token = context.Session.GetString("JwtToken");
                    refreshToken = context.Session.GetString("RefreshToken");

                    // If not in session, try cookies (for Remember Me scenarios after session expires)
                    if (string.IsNullOrEmpty(token))
                    {
                        token = context.Request.Cookies["jwt_token"];
                        refreshToken = context.Request.Cookies["refresh_token"];

                        // If found in cookies, restore to session for this request
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Session.SetString("JwtToken", token);
                            context.Session.SetString("RefreshToken", refreshToken ?? "");

                            // Also restore user data if available
                            var userData = context.Request.Cookies["user_data"];
                            if (!string.IsNullOrEmpty(userData))
                            {
                                context.Session.SetString("UserData", userData);
                            }

                            _logger.LogInformation("JWT token restored from cookies to session for user {UserId} accessing {RequestPath}",
                                userId, requestPath);
                        }
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogWarning("Authenticated user {UserId} missing JWT tokens while accessing {RequestPath}, redirecting to login",
                            userId, requestPath);
                        await SignOutAndRedirect(context);
                        return;
                    }

                    // Store token in HttpContext.Items for this request
                    context.Items["JwtToken"] = token;
                    context.Items["RefreshToken"] = refreshToken;

                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);
                        var tokenExpiry = jwtToken.ValidTo;
                        var timeUntilExpiry = tokenExpiry - DateTime.UtcNow;

                        // Log token status for monitoring
                        _logger.LogDebug("JWT token for user {UserId} expires at {TokenExpiry} (in {MinutesUntilExpiry} minutes)",
                            userId, tokenExpiry, timeUntilExpiry.TotalMinutes);

                        // Check if token is expired or expiring soon (within 5 minutes)
                        if (tokenExpiry < DateTime.UtcNow.AddMinutes(5))
                        {
                            _logger.LogInformation("JWT token expiring soon for user {UserId} (expires at {TokenExpiry}), attempting to refresh",
                                userId, tokenExpiry);

                            if (!string.IsNullOrEmpty(refreshToken))
                            {
                                try
                                {
                                    var refreshStartTime = DateTime.UtcNow;
                                    var result = await authService.RefreshTokenAsync(token, refreshToken);
                                    var refreshDuration = DateTime.UtcNow - refreshStartTime;

                                    if (result != null)
                                    {
                                        // Update tokens everywhere
                                        context.Items["JwtToken"] = result.AccessToken;
                                        context.Items["RefreshToken"] = result.RefreshToken;

                                        // Update session
                                        context.Session.SetString("JwtToken", result.AccessToken);
                                        context.Session.SetString("RefreshToken", result.RefreshToken);
                                        context.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));

                                        // Update cookies if they exist (Remember Me was used)
                                        if (context.Request.Cookies.ContainsKey("jwt_token"))
                                        {
                                            var cookieOptions = new CookieOptions
                                            {
                                                HttpOnly = true,
                                                Secure = context.Request.IsHttps,
                                                SameSite = SameSiteMode.Lax,
                                                Expires = DateTimeOffset.UtcNow.AddDays(30)
                                            };

                                            context.Response.Cookies.Append("jwt_token", result.AccessToken, cookieOptions);
                                            context.Response.Cookies.Append("refresh_token", result.RefreshToken, cookieOptions);
                                            context.Response.Cookies.Append("user_data", JsonConvert.SerializeObject(result.User), cookieOptions);

                                            _logger.LogDebug("Updated authentication cookies for user {UserId} after token refresh", userId);
                                        }

                                        _logger.LogInformation("Token refreshed successfully for user {UserId} in {RefreshDurationMs}ms",
                                            userId, refreshDuration.TotalMilliseconds);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Token refresh returned null for user {UserId} - signing out", userId);
                                        await SignOutAndRedirect(context);
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Token refresh failed for user {UserId}: {ErrorMessage} - signing out",
                                        userId, ex.Message);
                                    await SignOutAndRedirect(context);
                                    return;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("No refresh token available for user {UserId} - signing out", userId);
                                await SignOutAndRedirect(context);
                                return;
                            }
                        }
                        else
                        {
                            _logger.LogDebug("JWT token for user {UserId} is valid (expires in {MinutesUntilExpiry} minutes)",
                                userId, timeUntilExpiry.TotalMinutes);
                        }
                    }
                    else
                    {
                        _logger.LogError("Cannot read JWT token for user {UserId} - token format invalid", userId);
                        await SignOutAndRedirect(context);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                var userId = context.User?.Identity?.Name ?? "Unknown";
                var requestPath = context.Request.Path.Value ?? "Unknown";

                _logger.LogError(ex, "Unexpected error in JWT middleware for user {UserId} accessing {RequestPath}: {ErrorMessage}",
                    userId, requestPath, ex.Message);
            }

            await _next(context);
        }

        private async Task SignOutAndRedirect(HttpContext context)
        {
            var userId = context.User?.Identity?.Name ?? "Unknown";
            var requestPath = context.Request.Path.Value ?? "Unknown";

            _logger.LogInformation("Signing out user {UserId} and redirecting from {RequestPath} to login",
                userId, requestPath);

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();

            // Clear cookies if they exist
            var cookiesToClear = new[] { "jwt_token", "refresh_token", "user_data" };
            foreach (var cookieName in cookiesToClear)
            {
                if (context.Request.Cookies.ContainsKey(cookieName))
                {
                    context.Response.Cookies.Delete(cookieName);
                    _logger.LogDebug("Cleared authentication cookie {CookieName} for user {UserId}",
                        cookieName, userId);
                }
            }

            if (!context.Request.Path.StartsWithSegments("/Account/Login"))
            {
                var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
                _logger.LogInformation("Redirecting user {UserId} to login with return URL {ReturnUrl}",
                    userId, returnUrl);
                context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
            }
        }
    }
}