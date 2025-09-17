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
        private static readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
        private static DateTime _lastRefreshAttempt = DateTime.MinValue;
        private static readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(30);

        public TokenManager(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TokenManager> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Gets a valid token, refreshing if necessary
        /// This is the primary method that should be used by API calls
        /// </summary>
        public async Task<string?> GetValidTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                _logger.LogWarning("No HTTP context available for token validation");
                return null;
            }

            // First, try to get the current token
            var currentToken = GetCurrentToken(context);
            if (string.IsNullOrEmpty(currentToken))
            {
                _logger.LogDebug("No current token available");
                return null;
            }

            // Check if the token is still valid (not expired or close to expiring)
            if (IsTokenValid(currentToken))
            {
                _logger.LogDebug("Current token is still valid");
                return currentToken;
            }

            // Token is expired or close to expiring, attempt refresh
            _logger.LogInformation("Token is expired or close to expiring, attempting refresh");
            var refreshSuccess = await RefreshTokenAsync();

            if (refreshSuccess)
            {
                // Return the newly refreshed token
                var newToken = GetCurrentToken(context);
                _logger.LogInformation("Token successfully refreshed");
                return newToken;
            }

            _logger.LogWarning("Failed to refresh expired token");
            return null;
        }

        /// <summary>
        /// Refreshes the current token pair
        /// </summary>
        public async Task<bool> RefreshTokenAsync()
        {
            // Prevent multiple simultaneous refresh attempts
            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Token refresh already in progress, skipping");
                return false;
            }

            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null)
                {
                    _logger.LogError("No HTTP context available for token refresh");
                    return false;
                }

                // Implement cooldown to prevent rapid refresh attempts
                if (DateTime.UtcNow - _lastRefreshAttempt < _refreshCooldown)
                {
                    _logger.LogDebug("Refresh cooldown active, skipping refresh attempt");
                    return false;
                }

                _lastRefreshAttempt = DateTime.UtcNow;

                // Get current tokens from all possible sources
                var tokenInfo = GetTokenInfo(context);

                if (string.IsNullOrEmpty(tokenInfo.AccessToken) || string.IsNullOrEmpty(tokenInfo.RefreshToken))
                {
                    _logger.LogWarning("Missing access or refresh token for refresh attempt");
                    return false;
                }

                _logger.LogInformation("Attempting to refresh token");

                // Call the auth service to refresh tokens
                var newTokens = await _authService.RefreshTokenAsync(tokenInfo.AccessToken, tokenInfo.RefreshToken);

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh returned null or invalid tokens");
                    await ClearAllTokensAsync();
                    return false;
                }

                // Store the new tokens in all appropriate locations
                StoreTokens(newTokens, tokenInfo.RememberMe);

                _logger.LogInformation("Token refresh completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during token refresh");
                await ClearAllTokensAsync();
                return false;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        /// <summary>
        /// Stores tokens in session and optionally in cookies
        /// </summary>
        public void StoreTokens(TokenDto tokens)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Check if cookies exist to determine if this was a "Remember Me" login
            var rememberMe = context.Request.Cookies.ContainsKey("jwt_token");

            StoreTokens(tokens, rememberMe);
        }

        /// <summary>
        /// Stores tokens with explicit Remember Me flag
        /// </summary>
        private void StoreTokens(TokenDto tokens, bool rememberMe)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            try
            {
                // Always store in session for immediate availability
                context.Session.SetString("JwtToken", tokens.AccessToken);
                context.Session.SetString("RefreshToken", tokens.RefreshToken);
                context.Session.SetString("UserData", JsonConvert.SerializeObject(tokens.User));
                context.Session.SetString("TokenExpiry", tokens.ExpiresAt.ToString("O"));

                // Store in HttpContext.Items for immediate use within the same request
                context.Items["JwtToken"] = tokens.AccessToken;
                context.Items["RefreshToken"] = tokens.RefreshToken;

                // Store in cookies only if Remember Me was originally selected
                if (rememberMe)
                {
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(30) // 30-day cookie expiration
                    };

                    context.Response.Cookies.Append("jwt_token", tokens.AccessToken, cookieOptions);
                    context.Response.Cookies.Append("refresh_token", tokens.RefreshToken, cookieOptions);
                    context.Response.Cookies.Append("user_data", JsonConvert.SerializeObject(tokens.User), cookieOptions);

                    _logger.LogDebug("Tokens stored in session and cookies");
                }
                else
                {
                    _logger.LogDebug("Tokens stored in session only");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing tokens");
            }
        }

        /// <summary>
        /// Clears all tokens from session, cookies, and HTTP context
        /// </summary>
        public async Task ClearAllTokensAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            try
            {
                // Clear session
                context.Session.Clear();

                // Clear HttpContext.Items
                context.Items.Remove("JwtToken");
                context.Items.Remove("RefreshToken");

                // Clear cookies if they exist
                if (context.Request.Cookies.ContainsKey("jwt_token"))
                {
                    context.Response.Cookies.Delete("jwt_token");
                    context.Response.Cookies.Delete("refresh_token");
                    context.Response.Cookies.Delete("user_data");
                }

                _logger.LogInformation("All tokens cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing tokens");
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public void ClearSession()
        {
            ClearAllTokensAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the current token from the most reliable source
        /// </summary>
        private string? GetCurrentToken(HttpContext context)
        {
            // Priority order: HttpContext.Items (current request) > Session > Cookies
            return context.Items["JwtToken"] as string
                ?? context.Session.GetString("JwtToken")
                ?? context.Request.Cookies["jwt_token"];
        }

        /// <summary>
        /// Gets token information from all available sources
        /// </summary>
        private (string AccessToken, string RefreshToken, bool RememberMe) GetTokenInfo(HttpContext context)
        {
            var accessToken = context.Items["JwtToken"] as string
                            ?? context.Session.GetString("JwtToken")
                            ?? context.Request.Cookies["jwt_token"];

            var refreshToken = context.Items["RefreshToken"] as string
                             ?? context.Session.GetString("RefreshToken")
                             ?? context.Request.Cookies["refresh_token"];

            // If tokens came from cookies, this was a "Remember Me" login
            var rememberMe = !string.IsNullOrEmpty(context.Request.Cookies["jwt_token"]);

            // If we got tokens from cookies but they're not in session, restore to session
            if (!string.IsNullOrEmpty(accessToken) &&
                string.IsNullOrEmpty(context.Session.GetString("JwtToken")))
            {
                try
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

                    _logger.LogDebug("Restored tokens from cookies to session");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error restoring tokens from cookies to session");
                }
            }

            return (accessToken ?? "", refreshToken ?? "", rememberMe);
        }

        /// <summary>
        /// Checks if a token is valid and not close to expiring
        /// </summary>
        private bool IsTokenValid(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                {
                    _logger.LogWarning("Unable to read JWT token format");
                    return false;
                }

                var jwtToken = handler.ReadJwtToken(token);
                var expirationTime = jwtToken.ValidTo;
                var timeUntilExpiry = expirationTime - DateTime.UtcNow;

                // Consider token invalid if it expires within the next 2 minutes
                // This gives us a buffer to complete API calls before expiration
                var isValid = timeUntilExpiry.TotalMinutes > 2;

                if (!isValid)
                {
                    _logger.LogInformation("Token expires in {Minutes:F2} minutes, considered invalid",
                        timeUntilExpiry.TotalMinutes);
                }
                else
                {
                    _logger.LogDebug("Token valid for {Minutes:F2} more minutes",
                        timeUntilExpiry.TotalMinutes);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JWT token");
                return false; // If we can't parse it, consider it invalid
            }
        }
    }
}