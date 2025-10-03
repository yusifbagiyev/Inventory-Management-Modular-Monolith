using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace InventoryManagement.Web.Services
{
    public class TokenManager : ITokenManager
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenManager> _logger;

        // Thread-safe refresh lock to prevent race conditions
        private static readonly SemaphoreSlim _refreshLock = new(1, 1);

        // Track refresh attempts per session to prevent abuse
        private static readonly Dictionary<string, DateTime> _lastRefreshAttempts = new();
        private const int REFRESH_COOLDOWN_SECONDS = 5;

        // Security constants
        private const int TOKEN_EXPIRY_BUFFER_MINUTES = 5; // Refresh if less than 5 minutes remaining
        private const string TOKEN_VERSION_KEY = "TokenVersion"; // For token rotation tracking


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
            if (context == null)
            {
                _logger.LogWarning("No HttpContext available for token retrieval");
                return null;
            }

            // First check if user is authenticated at all
            if (!context.User?.Identity?.IsAuthenticated ?? true)
            {
                _logger.LogDebug("User is not authenticated, no token to retrieve");
                return null;
            }

            // Try to get the current token from multiple sources
            var token = GetTokenFromSecureStorage();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found in any storage location");
                await HandleAuthenticationFailureAsync();
                return null;
            }

            // Validate token integrity and expiration
            if (!IsTokenValid(token, out var timeUntilExpiry))
            {
                _logger.LogInformation("Token invalid or expired, attempting refresh");

                var refreshSuccess = await RefreshTokenAsync();
                if (refreshSuccess)
                {
                    token = GetTokenFromSecureStorage();
                    _logger.LogInformation("Token refreshed successfully");
                }
                else
                {
                    _logger.LogError("Token refresh failed, clearing authentication");
                    await HandleAuthenticationFailureAsync();
                    return null;
                }
            }
            else if (timeUntilExpiry.HasValue && timeUntilExpiry.Value.TotalMinutes < TOKEN_EXPIRY_BUFFER_MINUTES)
            {
                // Proactively refresh token before it expires
                _logger.LogInformation($"Token expiring in {timeUntilExpiry.Value.TotalMinutes:F1} minutes, proactively refreshing");

                // Fire and forget refresh to avoid blocking
                _ = Task.Run(async () => await RefreshTokenAsync());
            }

            return token;
        }


        public async Task<bool> RefreshTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return false;

            // Check cooldown to prevent refresh abuse
            var sessionId = context.Session.Id;
            if (IsInRefreshCooldown(sessionId))
            {
                _logger.LogDebug("Refresh attempt within cooldown period, skipping");
                return false;
            }

            // Acquire lock with timeout to prevent deadlocks
            if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("Could not acquire refresh lock within timeout");
                return false;
            }

            try
            {
                // Double-check token validity after acquiring lock
                var currentToken = GetTokenFromSecureStorage();
                if (IsTokenValid(currentToken, out _))
                {
                    _logger.LogDebug("Token is still valid after acquiring lock, skipping refresh");
                    return true;
                }

                // Get refresh token from secure storage
                var refreshToken = context.Session.GetString("RefreshToken");

                if (string.IsNullOrEmpty(currentToken) || string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogError("Missing tokens for refresh");
                    return false;
                }

                // Record refresh attempt
                RecordRefreshAttempt(sessionId);

                // Perform the actual refresh
                _logger.LogInformation("Calling auth service to refresh token");
                var newTokens = await _authService.RefreshTokenAsync(currentToken, refreshToken);

                if (newTokens == null || string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    _logger.LogError("Token refresh failed - invalid response from auth service");
                    return false;
                }

                // Store new tokens securely with rotation
                await StoreTokensSecurelyAsync(newTokens);

                _logger.LogInformation("Token refresh completed successfully");
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

        private async Task StoreTokensSecurelyAsync(TokenDto tokens)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Generate new token version for rotation tracking
            var tokenVersion = Guid.NewGuid().ToString();

            // Store in session (primary secure storage)
            context.Session.SetString("JwtToken", tokens.AccessToken);
            context.Session.SetString("RefreshToken", tokens.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokens.User));
            context.Session.SetString(TOKEN_VERSION_KEY, tokenVersion);

            // Store token version in HTTP-only cookie for client-side invalidation detection
            var versionCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict, // Strict for CSRF protection
                Expires = DateTimeOffset.UtcNow.AddDays(1) // Short-lived version cookie
            };

            context.Response.Cookies.Append("token_version", tokenVersion, versionCookieOptions);

            // Update user claims if needed
            await UpdateUserClaimsAsync(tokens.User);

            // Clear any client-side accessible storage for security
            ClearInsecureStorage();

            _logger.LogInformation("Tokens stored securely with version {TokenVersion}", tokenVersion);
        }

        private string? GetTokenFromSecureStorage()
        {
            var context = _httpContextAccessor.HttpContext;
            return context?.Session.GetString("JwtToken");
        }

        private bool IsTokenValid(string? token, out TimeSpan? timeUntilExpiry)
        {
            timeUntilExpiry = null;

            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                var handler = new JwtSecurityTokenHandler();

                // Basic JWT format validation
                if (!handler.CanReadToken(token))
                {
                    _logger.LogWarning("Token format is invalid");
                    return false;
                }

                var jwtToken = handler.ReadJwtToken(token);

                // Check token expiration
                var now = DateTime.UtcNow;
                var expiry = jwtToken.ValidTo;

                if (expiry <= now)
                {
                    _logger.LogInformation("Token has expired at {Expiry}", expiry);
                    return false;
                }

                timeUntilExpiry = expiry - now;

                // Validate token version for rotation
                var context = _httpContextAccessor.HttpContext;
                if (context != null)
                {
                    var sessionVersion = context.Session.GetString(TOKEN_VERSION_KEY);
                    var cookieVersion = context.Request.Cookies["token_version"];

                    if (!string.IsNullOrEmpty(sessionVersion) &&
                        !string.IsNullOrEmpty(cookieVersion) &&
                        sessionVersion != cookieVersion)
                    {
                        _logger.LogWarning("Token version mismatch detected, possible rotation issue");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JWT token");
                return false;
            }
        }


        private async Task HandleAuthenticationFailureAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            _logger.LogWarning("Handling authentication failure, clearing all auth state");

            // Clear server-side session
            context.Session.Clear();

            // Clear authentication cookie
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear any auth-related cookies
            var cookiesToClear = new[] { "token_version", ".AspNetCore.Session", ".InventoryManagement.Session" };
            foreach (var cookieName in cookiesToClear)
            {
                if (context.Request.Cookies.ContainsKey(cookieName))
                {
                    context.Response.Cookies.Delete(cookieName, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = context.Request.IsHttps,
                        SameSite = SameSiteMode.Strict
                    });
                }
            }
        }

        private async Task UpdateUserClaimsAsync(User user)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null || user == null) return;

            try
            {
                // Re-sign in with updated claims if user data changed
                var currentPrincipal = context.User;
                var nameIdentifier = currentPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (nameIdentifier == user.Id.ToString())
                {
                    // User is the same, update claims if needed
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim("FirstName", user.FirstName),
                        new Claim("LastName", user.LastName)
                    };

                    foreach (var role in user.Roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }

                    foreach (var permission in user.Permissions)
                    {
                        claims.Add(new Claim("permission", permission));
                    }

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
                    };

                    await context.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user claims");
            }
        }


        private void ClearInsecureStorage()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Remove any non-HttpOnly cookies that might contain tokens
            var insecureCookies = new[] { "jwt_token", "refresh_token", "user_data" };
            foreach (var cookieName in insecureCookies)
            {
                if (context.Request.Cookies.ContainsKey(cookieName))
                {
                    context.Response.Cookies.Delete(cookieName);
                }
            }

            // Clear HttpContext.Items (only valid for current request anyway)
            context.Items.Remove("JwtToken");
        }


        private bool IsInRefreshCooldown(string sessionId)
        {
            lock (_lastRefreshAttempts)
            {
                if (_lastRefreshAttempts.TryGetValue(sessionId, out var lastAttempt))
                {
                    var timeSinceLastAttempt = DateTime.UtcNow - lastAttempt;
                    return timeSinceLastAttempt.TotalSeconds < REFRESH_COOLDOWN_SECONDS;
                }
                return false;
            }
        }


        private void RecordRefreshAttempt(string sessionId)
        {
            lock (_lastRefreshAttempts)
            {
                _lastRefreshAttempts[sessionId] = DateTime.UtcNow;

                // Clean up old entries to prevent memory leak
                var cutoffTime = DateTime.UtcNow.AddMinutes(-30);
                var keysToRemove = _lastRefreshAttempts
                    .Where(kvp => kvp.Value < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _lastRefreshAttempts.Remove(key);
                }
            }
        }


        public async Task ClearAllTokensAsync()
        {
            await HandleAuthenticationFailureAsync();
        }
    }
}