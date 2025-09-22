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

        // Thread-safe token refresh management
        private static readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
        private static DateTime _lastRefreshAttempt = DateTime.MinValue;
        private static DateTime _lastSuccessfulRefresh = DateTime.MinValue;
        private static readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _minRefreshInterval = TimeSpan.FromSeconds(5);
        private const int RefreshTimeoutSeconds = 10;


        // Cache for tracking ongoing refresh operations
        private static readonly Dictionary<string, Task<bool>> _refreshOperations = new();
        private static readonly object _refreshLock = new object();

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
        /// This method is designed to handle concurrent calls efficiently
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

            // Check if the token is still valid
            var tokenValidation = ValidateToken(currentToken);
            if (tokenValidation.IsValid)
            {
                _logger.LogDebug("Current token is still valid for {Minutes:F2} minutes",
                    tokenValidation.TimeUntilExpiry.TotalMinutes);
                return currentToken;
            }

            // Use timeout for refresh operation
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(RefreshTimeoutSeconds));


            try
            {
                _logger.LogInformation("Token expired or expiring soon, attempting refresh");
                var refreshSuccess = await RefreshTokenWithTimeoutAsync(context, cts.Token);

                if (refreshSuccess)
                {
                    var newToken = GetCurrentToken(context);
                    _logger.LogInformation("Token successfully refreshed");
                    return newToken;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Token refresh operation timed out after {Timeout} seconds", RefreshTimeoutSeconds);
            }

            _logger.LogWarning("Failed to refresh expired token");
            return null;
        }


        private async Task<bool> RefreshTokenWithTimeoutAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // Try to acquire the semaphore with timeout
            if (!await _refreshSemaphore.WaitAsync(100, cancellationToken))
            {
                _logger.LogDebug("Could not acquire refresh semaphore, another refresh in progress");

                // Wait a bit and check if refresh succeeded
                await Task.Delay(500, cancellationToken);
                return DateTime.UtcNow - _lastRefreshAttempt < TimeSpan.FromSeconds(5);
            }

            try
            {
                // Check if we recently refreshed
                if (DateTime.UtcNow - _lastRefreshAttempt < TimeSpan.FromSeconds(5))
                {
                    _logger.LogDebug("Token was recently refreshed, skipping");
                    return true;
                }

                _lastRefreshAttempt = DateTime.UtcNow;

                var tokenInfo = GetTokenInfo(context);
                if (string.IsNullOrEmpty(tokenInfo.AccessToken) ||
                    string.IsNullOrEmpty(tokenInfo.RefreshToken))
                {
                    _logger.LogWarning("Missing tokens for refresh");
                    return false;
                }

                _logger.LogInformation("Attempting to refresh token");

                // Call auth service with cancellation token support
                var refreshTask = _authService.RefreshTokenAsync(
                    tokenInfo.AccessToken,
                    tokenInfo.RefreshToken);

                var completedTask = await Task.WhenAny(
                    refreshTask,
                    Task.Delay(TimeSpan.FromSeconds(RefreshTimeoutSeconds), cancellationToken));

                if (completedTask != refreshTask)
                {
                    _logger.LogError("Token refresh timed out");
                    return false;
                }

                var newTokens = await refreshTask;

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh returned invalid tokens");
                    return false;
                }

                StoreTokens(newTokens, tokenInfo.RememberMe);
                return true;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }


        /// <summary>
        /// Coordinates token refresh to prevent multiple simultaneous refresh attempts
        /// </summary>
        private async Task<bool> CoordinatedRefreshAsync(HttpContext context)
        {
            var sessionId = context.Session.Id;

            lock (_refreshLock)
            {
                // Check if a refresh is already in progress for this session
                if (_refreshOperations.ContainsKey(sessionId))
                {
                    _logger.LogDebug("Refresh already in progress for session {SessionId}, waiting...", sessionId);
                    return _refreshOperations[sessionId].Result;
                }

                // Check if we recently did a successful refresh (within 5 seconds)
                if (DateTime.UtcNow - _lastSuccessfulRefresh < _minRefreshInterval)
                {
                    _logger.LogDebug("Recent successful refresh detected, skipping new refresh attempt");
                    // Check if the token was actually refreshed
                    var currentToken = GetCurrentToken(context);
                    var validation = ValidateToken(currentToken);
                    return validation.IsValid;
                }

                // Start a new refresh operation
                var refreshTask = PerformRefreshAsync();
                _refreshOperations[sessionId] = refreshTask;

                // Clean up the operation after completion
                refreshTask.ContinueWith(t =>
                {
                    lock (_refreshLock)
                    {
                        _refreshOperations.Remove(sessionId);
                    }
                });

                return refreshTask.Result;
            }
        }



        /// <summary>
        /// Performs the actual token refresh
        /// </summary>
        private async Task<bool> PerformRefreshAsync()
        {
            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Could not acquire refresh semaphore, another refresh in progress");
                // Wait a bit and check if refresh succeeded
                await Task.Delay(500);
                return DateTime.UtcNow - _lastSuccessfulRefresh < TimeSpan.FromSeconds(10);
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

                if (string.IsNullOrEmpty(tokenInfo.AccessToken) ||
                    string.IsNullOrEmpty(tokenInfo.RefreshToken))
                {
                    _logger.LogWarning("Missing access or refresh token for refresh attempt");
                    return false;
                }

                _logger.LogInformation("Attempting to refresh token");

                // Call the auth service to refresh tokens
                var newTokens = await _authService.RefreshTokenAsync(
                    tokenInfo.AccessToken,
                    tokenInfo.RefreshToken);

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh returned null or invalid tokens");
                    await ClearAllTokensAsync();
                    return false;
                }

                // Store the new tokens in all appropriate locations
                StoreTokens(newTokens, tokenInfo.RememberMe);

                _lastSuccessfulRefresh = DateTime.UtcNow;
                _logger.LogInformation("Token refresh completed successfully");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during token refresh");

                // Only clear tokens if this is a permanent failure (e.g., refresh token expired)
                if (ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                {
                    await ClearAllTokensAsync();
                }

                return false;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }



        /// <summary>
        /// Refreshes the current token pair (backward compatibility)
        /// </summary>
        public async Task<bool> RefreshTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return false;

            return await CoordinatedRefreshAsync(context);
        }



        /// <summary>
        /// Validates a token and returns detailed information
        /// </summary>
        private (bool IsValid, TimeSpan TimeUntilExpiry) ValidateToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                {
                    _logger.LogWarning("Unable to read JWT token format");
                    return (false, TimeSpan.Zero);
                }

                var jwtToken = handler.ReadJwtToken(token);
                var expirationTime = jwtToken.ValidTo;
                var timeUntilExpiry = expirationTime - DateTime.UtcNow;

                // Consider token invalid if it expires within the next 2 minutes
                // This gives us a buffer to complete API calls before expiration
                var isValid = timeUntilExpiry.TotalMinutes > 2;

                return (isValid, timeUntilExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JWT token");
                return (false, TimeSpan.Zero);
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
                        Expires = DateTimeOffset.UtcNow.AddDays(30)
                    };

                    context.Response.Cookies.Append("jwt_token", tokens.AccessToken, cookieOptions);
                    context.Response.Cookies.Append("refresh_token", tokens.RefreshToken, cookieOptions);
                    context.Response.Cookies.Append("user_data",
                        JsonConvert.SerializeObject(tokens.User), cookieOptions);

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
    }
}