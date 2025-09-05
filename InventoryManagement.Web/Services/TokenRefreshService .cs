using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Services
{
    public class TokenRefreshService : ITokenRefreshService
    {
        private readonly IAuthService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TokenRefreshService> _logger;
        private static readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
        private static DateTime _lastRefreshAttempt = DateTime.MinValue;
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

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogDebug("No tokens available for refresh");
                return null;
            }

            // Check if token needs refresh
            if (!IsTokenExpiringSoon(accessToken))
            {
                return null;
            }

            // Prevent multiple concurrent refresh attempts
            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Token refresh already in progress");
                return null;
            }

            try
            {
                // Rate limiting protection
                var timeSinceLastAttempt = DateTime.UtcNow - _lastRefreshAttempt;
                if (timeSinceLastAttempt < _minRefreshInterval)
                {
                    _logger.LogDebug("Refresh rate limited, last attempt was {Seconds}s ago",
                        timeSinceLastAttempt.TotalSeconds);
                    return null;
                }

                _lastRefreshAttempt = DateTime.UtcNow;

                // Double-check token still needs refresh (might have been refreshed by another thread)
                var currentToken = GetCurrentAccessToken(context);
                if (currentToken != accessToken || !IsTokenExpiringSoon(currentToken))
                {
                    _logger.LogDebug("Token already refreshed by another process");
                    return null;
                }

                _logger.LogInformation("Attempting token refresh");
                var newTokens = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (newTokens != null && !string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    UpdateTokensEverywhere(newTokens);
                    _logger.LogInformation("Token refreshed successfully");
                    return newTokens;
                }
                else
                {
                    _logger.LogWarning("Token refresh returned null or empty token");
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
                    return false;

                var jwtToken = handler.ReadJwtToken(token);
                var timeUntilExpiry = jwtToken.ValidTo - DateTime.UtcNow;

                // Refresh if token expires in less than 2 minutes
                return timeUntilExpiry.TotalMinutes <= 2 && timeUntilExpiry.TotalMinutes > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token expiration");
                return false;
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
            if (context.Request.Cookies.ContainsKey("jwt_token"))
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
