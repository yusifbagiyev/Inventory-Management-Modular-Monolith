using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;

namespace InventoryManagement.Web.Services
{
    public class TokenRefreshService : ITokenRefreshService
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenRefreshService> _logger;
        private static readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
        private static readonly Dictionary<string, DateTime> _lastRefreshAttempts = new();
        private static readonly TimeSpan _minRefreshInterval = TimeSpan.FromSeconds(30);

        public TokenRefreshService(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TokenRefreshService> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<TokenDto?> RefreshTokenIfNeededAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            var accessToken = GetCurrentAccessToken(context);
            var refreshToken = GetCurrentRefreshToken(context);

            // If we have no tokens at all, clear authentication
            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
            {
                await ClearAuthenticationAsync(context);
                return null;
            }

            // If we only have a refresh token (access token missing or expired)
            if (string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogInformation("Access token missing but refresh token available, attempting refresh");
                return await PerformTokenRefresh("", refreshToken);
            }

            // Check if access token is expired or expiring soon
            if (!string.IsNullOrEmpty(accessToken))
            {
                if (IsTokenExpired(accessToken))
                {
                    _logger.LogInformation("Access token expired, attempting refresh");
                    return await PerformTokenRefresh(accessToken, refreshToken);
                }

                if (!IsTokenExpiringSoon(accessToken))
                {
                    return null; // Token is still valid
                }
            }

            // Check rate limiting
            var userKey = context.User?.Identity?.Name ?? "anonymous";
            lock (_lastRefreshAttempts)
            {
                if (_lastRefreshAttempts.TryGetValue(userKey, out var lastAttempt))
                {
                    if (DateTime.UtcNow - lastAttempt < _minRefreshInterval)
                    {
                        _logger.LogDebug("Refresh rate limited for user {User}", userKey);
                        return null;
                    }
                }
            }

            return await PerformTokenRefresh(accessToken, refreshToken);
        }

        private async Task<TokenDto?> PerformTokenRefresh(string accessToken, string? refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Cannot refresh token: refresh token is missing");
                return null;
            }

            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Token refresh already in progress");
                return null;
            }

            try
            {
                var context = _httpContextAccessor.HttpContext;
                var userKey = context?.User?.Identity?.Name ?? "anonymous";

                // Update last attempt time
                lock (_lastRefreshAttempts)
                {
                    _lastRefreshAttempts[userKey] = DateTime.UtcNow;

                    // Clean up old entries
                    var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
                    var keysToRemove = _lastRefreshAttempts
                        .Where(kvp => kvp.Value < cutoff)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in keysToRemove)
                    {
                        _lastRefreshAttempts.Remove(key);
                    }
                }

                _logger.LogInformation("Attempting token refresh for user {User}", userKey);

                var newTokens = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (newTokens != null && !string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    UpdateTokensEverywhere(newTokens);
                    _logger.LogInformation("Token refreshed successfully for user {User}", userKey);
                    return newTokens;
                }
                else
                {
                    _logger.LogWarning("Token refresh failed for user {User}", userKey);

                    // If refresh failed, clear authentication
                    if (context != null)
                    {
                        await ClearAuthenticationAsync(context);
                    }

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return null;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        public bool IsTokenExpiringSoon(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                    return true; // If we can't read it, consider it expiring

                var jwtToken = handler.ReadJwtToken(token);
                var timeUntilExpiry = jwtToken.ValidTo - DateTime.UtcNow;

                // Refresh if token expires in less than 5 minutes
                return timeUntilExpiry.TotalMinutes <= 5;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token expiration");
                return true; // If there's an error, consider it expiring
            }
        }

        private bool IsTokenExpired(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                    return true;

                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.ValidTo < DateTime.UtcNow;
            }
            catch
            {
                return true;
            }
        }

        public void UpdateTokensEverywhere(TokenDto tokenDto)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Update session
            context.Session.SetString("JwtToken", tokenDto.AccessToken);
            context.Session.SetString("RefreshToken", tokenDto.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokenDto.User));

            // Update HttpContext.Items for immediate use
            context.Items["JwtToken"] = tokenDto.AccessToken;
            context.Items["RefreshToken"] = tokenDto.RefreshToken;

            // Update cookies if Remember Me was used
            var rememberMe = context.User?.FindFirst("RememberMe")?.Value == "True";
            if (rememberMe || context.Request.Cookies.ContainsKey("jwt_token"))
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddDays(30)
                };

                context.Response.Cookies.Append("jwt_token", tokenDto.AccessToken, cookieOptions);
                context.Response.Cookies.Append("refresh_token", tokenDto.RefreshToken, cookieOptions);
                context.Response.Cookies.Append("user_data", JsonConvert.SerializeObject(tokenDto.User), cookieOptions);
            }
        }

        private async Task ClearAuthenticationAsync(HttpContext context)
        {
            _logger.LogInformation("Clearing authentication due to invalid tokens");

            // Clear session
            context.Session.Clear();

            // Clear cookies
            context.Response.Cookies.Delete("jwt_token");
            context.Response.Cookies.Delete("refresh_token");
            context.Response.Cookies.Delete("user_data");

            // Sign out from cookie authentication
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        private string? GetCurrentAccessToken(HttpContext context)
        {
            return context.Items["JwtToken"] as string
                ?? context.Session.GetString("JwtToken")
                ?? context.Request.Cookies["jwt_token"];
        }

        private string? GetCurrentRefreshToken(HttpContext context)
        {
            return context.Items["RefreshToken"] as string
                ?? context.Session.GetString("RefreshToken")
                ?? context.Request.Cookies["refresh_token"];
        }
    }
}