using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class TokenManager : ITokenManager
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenManager> _logger;
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);
        private static DateTime _lastRefreshAttempt = DateTime.MinValue;
        private const int REFRESH_COOLDOWN_SECONDS = 5;

        // Track when the user's 30-day session should end (for Remember Me users)
        private const int MAX_SESSION_DAYS = 30;

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

            // Check if user's session has exceeded 30 days (for Remember Me users)
            if (await HasSessionExpiredAsync())
            {
                _logger.LogInformation("User session exceeded 30 days, forcing logout");
                await ClearAllTokensAsync();
                return null;
            }

            // Get current token from session (server-side storage)
            var token = context.Session.GetString("JwtToken");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found in session");
                return null;
            }

            // Check if token needs refresh
            if (IsTokenExpiredOrExpiring(token))
            {
                _logger.LogInformation("Token expired or expiring soon, attempting refresh...");
                var refreshSuccess = await RefreshTokenAsync();

                if (refreshSuccess)
                {
                    // Get the newly refreshed token
                    token = context.Session.GetString("JwtToken");
                    _logger.LogInformation("Successfully retrieved refreshed token");
                }
                else
                {
                    _logger.LogWarning("Token refresh failed, clearing invalid tokens");
                    await ClearAllTokensAsync();
                    return null;
                }
            }

            // Update last activity time
            context.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

            return token;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            // Implement cooldown
            var timeSinceLastRefresh = DateTime.Now - _lastRefreshAttempt;
            if (timeSinceLastRefresh.TotalSeconds < REFRESH_COOLDOWN_SECONDS)
            {
                _logger.LogDebug("Refresh attempted too soon, skipping");
                return false;
            }

            if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("Could not acquire refresh lock within timeout");
                return false;
            }

            try
            {
                _lastRefreshAttempt = DateTime.Now;

                var context = _httpContextAccessor.HttpContext;
                if (context == null) return false;

                // Get refresh token from HttpOnly cookie
                var refreshToken = context.Request.Cookies["refresh_token"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("No refresh token available");
                    return false;
                }

                _logger.LogInformation("Calling auth service to refresh token...");
                var newTokens = await _authService.RefreshTokenAsync(refreshToken);

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh failed - received invalid response");
                    return false;
                }

                // Store new access token in session only
                context.Session.SetString("JwtToken", newTokens.AccessToken);
                context.Session.SetString("LastActivity", DateTime.Now.ToString("o"));

                // Update refresh token cookie (token rotation)
                var rememberMe = context.Request.Cookies["remember_me"] == "true";
                var refreshCookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = rememberMe
                        ? DateTimeOffset.Now.AddDays(30)
                        : DateTimeOffset.Now.AddHours(1),
                    Path = "/",
                    IsEssential = true
                };
                context.Response.Cookies.Append("refresh_token", newTokens.RefreshToken, refreshCookieOptions);

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

        private bool IsTokenExpiredOrExpiring(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var expiryTime = jwtToken.ValidTo;
                var now = DateTime.Now;

                // Consider expired if less than 5 minutes remaining
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
                return true;
            }
        }

        private async Task<bool> HasSessionExpiredAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return false;

            // Only check for Remember Me users
            var rememberMe = context.Request.Cookies["remember_me"] == "true";
            if (!rememberMe) return false;

            // Get login time from claims
            var loginTimeClaim = context.User.FindFirst("LoginTime");
            if (loginTimeClaim == null) return false;

            if (DateTime.TryParse(loginTimeClaim.Value, out DateTime loginTime))
            {
                var sessionAge = DateTime.Now - loginTime;
                if (sessionAge.TotalDays >= MAX_SESSION_DAYS)
                {
                    return true;
                }
            }
            await Task.Delay(1);
            return false;
        }

        public async Task ClearAllTokensAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            _logger.LogInformation("Clearing all stored tokens");

            // Clear session
            context.Session.Remove("JwtToken");
            context.Session.Remove("UserData");
            context.Session.Remove("LastActivity");

            // Clear refresh token cookie
            context.Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

            // Note: We intentionally do NOT clear remember_me and username cookies
            // This allows the user to be remembered without keeping their session active

            await Task.CompletedTask;
        }
    }
}