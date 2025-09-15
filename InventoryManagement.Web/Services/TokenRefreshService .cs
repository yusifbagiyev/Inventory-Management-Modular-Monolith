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

            // Get current tokens from session (primary source)
            var accessToken = context.Session.GetString("JwtToken");
            var refreshToken = context.Session.GetString("RefreshToken");

            // Fallback to cookies if session is empty
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                accessToken = context.Request.Cookies["jwt_token"];
                refreshToken = context.Request.Cookies["refresh_token"];
            }

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No tokens available for refresh");
                return null;
            }

            // Acquire semaphore to prevent concurrent refreshes
            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Token refresh already in progress");
                return null;
            }

            try
            {
                _logger.LogInformation("Attempting to refresh token");

                var newTokens = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (newTokens != null && !string.IsNullOrEmpty(newTokens.AccessToken))
                {
                    UpdateTokensEverywhere(newTokens);
                    _logger.LogInformation("Token refreshed successfully");
                    return newTokens;
                }

                _logger.LogWarning("Token refresh failed");
                return null;
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
                    return true;

                var jwtToken = handler.ReadJwtToken(token);
                var timeUntilExpiry = jwtToken.ValidTo - DateTime.UtcNow;

                // Refresh if less than 5 minutes remaining
                return timeUntilExpiry.TotalMinutes <= 5;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token expiration");
                return true;
            }
        }

        public void UpdateTokensEverywhere(TokenDto tokenDto)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Update session (primary storage)
            context.Session.SetString("JwtToken", tokenDto.AccessToken);
            context.Session.SetString("RefreshToken", tokenDto.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokenDto.User));

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
                context.Response.Cookies.Append("user_data",
                    JsonConvert.SerializeObject(tokenDto.User), cookieOptions);
            }

            // Update HttpContext.Items for immediate use
            context.Items["JwtToken"] = tokenDto.AccessToken;
            context.Items["RefreshToken"] = tokenDto.RefreshToken;
        }
    }
}