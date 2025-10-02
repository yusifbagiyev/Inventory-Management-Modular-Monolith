using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Services
{
    public class TokenManager : ITokenManager
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenManager> _logger;
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);
        private static DateTime _lastRefreshAttempt = DateTime.MinValue;
        private const int REFRESH_COOLDOWN_SECONDS = 5; // Prevent rapid refresh attempts

        public TokenManager(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TokenManager> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<string?> GetValidTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // Try to get the current token from multiple sources
            var token = GetCurrentToken();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found in any storage location");
                return null;
            }

            // Check if the token needs refresh (with buffer time)
            if (IsTokenExpiredOrExpiring(token))
            {
                _logger.LogInformation("Token expired or expiring soon, attempting refresh...");
                var refreshSuccess = await RefreshTokenAsync();

                if (refreshSuccess)
                {
                    // Get the newly refreshed token
                    token = GetCurrentToken();
                    _logger.LogInformation("Successfully retrieved refreshed token");
                }
                else
                {
                    _logger.LogWarning("Token refresh failed, clearing invalid tokens");
                    await ClearAllTokensAsync();
                    return null;
                }
            }

            return token;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            // Implement cooldown to prevent rapid refresh attempts
            var timeSinceLastRefresh = DateTime.UtcNow - _lastRefreshAttempt;
            if (timeSinceLastRefresh.TotalSeconds < REFRESH_COOLDOWN_SECONDS)
            {
                _logger.LogDebug("Refresh attempted too soon, skipping");
                return false;
            }

            // Try to acquire the lock with a timeout
            if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("Could not acquire refresh lock within timeout");
                return false;
            }

            try
            {
                _lastRefreshAttempt = DateTime.UtcNow;

                var context = _httpContextAccessor.HttpContext;
                if (context == null) return false;

                // Get tokens from all possible sources (fixed the typo here)
                var accessToken = context.Session.GetString("JwtToken")
                    ?? context.Request.Cookies["jwt_token"];

                var refreshToken = context.Session.GetString("RefreshToken")
                    ?? context.Request.Cookies["refresh_token"];

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("Missing tokens for refresh - Access: {HasAccess}, Refresh: {HasRefresh}",
                        !string.IsNullOrEmpty(accessToken), !string.IsNullOrEmpty(refreshToken));
                    return false;
                }

                _logger.LogInformation("Calling auth service to refresh token...");
                var newTokens = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh failed - received invalid response");
                    return false;
                }

                // Store the new tokens in all locations
                await StoreTokensEverywhere(newTokens);
                _logger.LogInformation("Token refreshed and stored successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                return false;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task StoreTokensEverywhere(TokenDto tokens)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Store in session (primary storage for immediate use)
            context.Session.SetString("JwtToken", tokens.AccessToken);
            context.Session.SetString("RefreshToken", tokens.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokens.User));

            // Store in cookies for persistence across browser sessions
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7) // Cookies last 7 days
            };

            context.Response.Cookies.Append("jwt_token", tokens.AccessToken, cookieOptions);
            context.Response.Cookies.Append("refresh_token", tokens.RefreshToken, cookieOptions);
            context.Response.Cookies.Append("user_data",
                JsonConvert.SerializeObject(tokens.User), cookieOptions);

            // Also update HttpContext.Items for immediate use in the current request
            context.Items["JwtToken"] = tokens.AccessToken;

            await Task.CompletedTask; // Ensure async context
        }

        private bool IsTokenExpiredOrExpiring(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Get expiration time
                var expiryTime = jwtToken.ValidTo;
                var now = DateTime.UtcNow;

                // Consider expired if less than 5 minutes remaining (buffer time)
                var bufferTime = TimeSpan.FromMinutes(5);
                var expiresIn = expiryTime - now;

                if (expiresIn <= bufferTime)
                {
                    _logger.LogInformation("Token expires at {ExpiryTime}, current time {Now}, expires in {ExpiresIn}",
                        expiryTime, now, expiresIn);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JWT token");
                return true; // Consider expired if we can't parse it
            }
        }

        private string? GetCurrentToken()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // Check multiple sources in priority order
            // 1. HttpContext.Items (set by middleware for current request)
            if (context.Items.TryGetValue("JwtToken", out var itemToken) && itemToken is string token1)
            {
                return token1;
            }

            // 2. Session (server-side storage)
            var sessionToken = context.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(sessionToken))
            {
                return sessionToken;
            }

            // 3. Cookies (client-side persistent storage)
            var cookieToken = context.Request.Cookies["jwt_token"];
            if (!string.IsNullOrEmpty(cookieToken))
            {
                return cookieToken;
            }

            return null;
        }

        public async Task ClearAllTokensAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            _logger.LogInformation("Clearing all stored tokens");

            // Clear session
            context.Session.Remove("JwtToken");
            context.Session.Remove("RefreshToken");
            context.Session.Remove("UserData");

            // Clear cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(-1) // Expire immediately
            };

            context.Response.Cookies.Delete("jwt_token", cookieOptions);
            context.Response.Cookies.Delete("refresh_token", cookieOptions);
            context.Response.Cookies.Delete("user_data", cookieOptions);

            // Clear HttpContext.Items
            context.Items.Remove("JwtToken");

            await Task.CompletedTask;
        }

        // Compatibility method
        public void StoreTokens(TokenDto tokens) =>
            StoreTokensEverywhere(tokens).GetAwaiter().GetResult();
    }
}