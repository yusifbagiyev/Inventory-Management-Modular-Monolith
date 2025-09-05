using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;

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

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            // Skip for static files 
            if (IsStaticFileRequest(context))
            {
                await _next(context);
                return;
            }
            // Only process once per request
            if (context.Items.ContainsKey("JwtMiddlewareProcessed"))
            {
                await _next(context);
                return;
            }
            context.Items["JwtMiddlewareProcessed"] = true;

            // Skip authentication pages
            if (context.Request.Path.StartsWithSegments("/Account/Login") ||
                context.Request.Path.StartsWithSegments("/Account/Logout") ||
                context.Request.Path.StartsWithSegments("/Account/AccessDenied"))
            {
                await _next(context);
                return;
            }

            // Only check authenticated users
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                await TryRefreshTokenIfNeeded(context, authService);
            }

            await _next(context);
        }
        private async Task TryRefreshTokenIfNeeded(HttpContext context, IAuthService authService)
        {
            try
            {
                var tokenInfo = GetTokenInfo(context);

                if (string.IsNullOrEmpty(tokenInfo.AccessToken) ||
                    string.IsNullOrEmpty(tokenInfo.RefreshToken))
                {
                    _logger.LogDebug("No tokens available for refresh check");
                    return;
                }

                // Fix: Check token expiration more accurately
                var tokenExpiry = GetTokenExpiration(tokenInfo.AccessToken);
                if (tokenExpiry == null)
                {
                    _logger.LogWarning("Could not parse token expiration");
                    return;
                }

                var timeUntilExpiry = tokenExpiry.Value - DateTime.UtcNow;

                // Only refresh if token expires in less than 2 minutes
                if (timeUntilExpiry.TotalMinutes <= 2 && timeUntilExpiry.TotalMinutes > 0)
                {
                    _logger.LogInformation("Token expiring in {Minutes:F2} minutes, attempting refresh",
                        timeUntilExpiry.TotalMinutes);

                    var success = await RefreshToken(context, authService,
                        tokenInfo.AccessToken, tokenInfo.RefreshToken);

                    if (success)
                    {
                        _logger.LogInformation("Token refreshed successfully");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh token");
                    }
                }
                else
                {
                    _logger.LogDebug("Token still valid for {Minutes:F2} minutes, no refresh needed",
                        timeUntilExpiry.TotalMinutes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in token refresh check");
            }
        }

        private DateTime? GetTokenExpiration(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                    return null;

                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.ValidTo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing token expiration");
                return null;
            }
        }


        private (string AccessToken, string RefreshToken,bool RememberMe) GetTokenInfo(HttpContext context)
        {
            var accessToken = context.Session.GetString("JwtToken");
            var refreshToken = context.Session.GetString("RefreshToken");
            var rememberMe = false;

            // If not in session, check cookies
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = context.Request.Cookies["jwt_token"];
                refreshToken = context.Request.Cookies["refresh_token"];
                rememberMe = !string.IsNullOrEmpty(accessToken);

                // Restore to session from cookies
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Session.SetString("JwtToken", accessToken);
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        context.Session.SetString("RefreshToken", refreshToken);
                    }
                    var userData = context.Request.Cookies["user_data"];
                    if (!string.IsNullOrEmpty(userData))
                    {
                        context.Session.SetString("UserData", userData);
                    }
                }
            }
            return (accessToken ?? "", refreshToken ?? "",rememberMe);
        }


        private async Task<bool> RefreshToken(
            HttpContext context,
            IAuthService authService,
            string accessToken,
            string refreshToken)
        {
            try
            {
                var result = await authService.RefreshTokenAsync(accessToken, refreshToken);

                if (result != null && !string.IsNullOrEmpty(result.AccessToken))
                {
                    UpdateTokensEverywhere(context, result);
                    _logger.LogInformation("Token refreshed successfully for user {User}",
                        result.User?.Username ?? "Unknown");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Token refresh failed - null result");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return false;
            }
        }


        private void UpdateTokensEverywhere(HttpContext context, TokenDto tokenDto)
        {
            // Update session
            context.Session.SetString("JwtToken", tokenDto.AccessToken);
            context.Session.SetString("RefreshToken", tokenDto.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokenDto.User));

            // Update cookies if Remember Me was used (check for existing cookies)
            if (context.Request.Cookies.ContainsKey("jwt_token"))
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddDays(30)  // 30-day cookie
                };

                context.Response.Cookies.Append("jwt_token", tokenDto.AccessToken, cookieOptions);
                context.Response.Cookies.Append("refresh_token", tokenDto.RefreshToken, cookieOptions);
                context.Response.Cookies.Append("user_data",
                    JsonConvert.SerializeObject(tokenDto.User), cookieOptions);
            }

            // Update HttpContext.Items for immediate use
            context.Items["JwtToken"] = tokenDto.AccessToken;
            context.Items["RefreshToken"] = tokenDto.RefreshToken;
        }

        private bool IsStaticFileRequest(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var staticExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg",
                                          ".gif", ".ico", ".woff", ".woff2", ".ttf", ".svg" };
            return staticExtensions.Any(ext => path.EndsWith(ext));
        }

    }
}