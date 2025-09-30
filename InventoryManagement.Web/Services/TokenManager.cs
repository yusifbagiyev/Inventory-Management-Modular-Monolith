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

            // First, try to get the current token
            var token = context.Request.Cookies["jwt_token"]
                        ?? context.Session.GetString("JwtToken");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No token found");
                return null;
            }

            // Check if the token is still valid
            if (IsTokenExpired(token))
            {
                _logger.LogInformation("Token expired, refreshing...");
                var refreshSuccess= await RefreshTokenAsync();

                if(refreshSuccess)
                {
                    token=context.Request.Cookies["jwt_token"]
                        ?? context.Session.GetString("JwtToken");
                }
                else
                {
                    _logger.LogWarning("Token refresh failed");
                    return null;
                }
            }

            return token;
        }


        public async Task<bool> RefreshTokenAsync()
        {
            // Prevent multiple simultaneous refreshes
            if (!await _refreshLock.WaitAsync(100))
            {
                await Task.Delay(500); // Wait for the other refresh to complete
                var token = GetCurrentToken();
                return !string.IsNullOrEmpty(token) && !IsTokenExpired(token);
            }

            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return false;

                var accessToken = context.Request.Cookies["jwt_token"]
                        ?? context.Session.GetString("Jwt_token");
                var refreshToken = context.Request.Cookies["refresh_token"]
                        ?? context.Session.GetString("Refresh_token");

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("Missing access or refresh token");
                    return false;
                }

                // Call the auth service to refresh the token
                var newTokens = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                if (newTokens == null)
                {
                    _logger.LogError("Token refresh failed, received null tokens");
                    await ClearAllTokensAsync();
                    return false;
                }

                // Store the new tokens simply in both cookie and session
                StoreTokens(newTokens);
                _logger.LogInformation("Token refreshed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return false;
            }
            finally
            {
                _refreshLock.Release();
            }
        }


        private void StoreTokensSimple(TokenDto tokens)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Always store in session for immediate use
            context.Session.SetString("JwtToken", tokens.AccessToken);
            context.Session.SetString("RefreshToken", tokens.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokens.User));


            // Always store in cookies for persistence
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddDays(30)
            };

            context.Response.Cookies.Append("jwt_token", tokens.AccessToken, cookieOptions);
            context.Response.Cookies.Append("refresh_token", tokens.RefreshToken, cookieOptions);
            context.Response.Cookies.Append("user_data",
                JsonConvert.SerializeObject(tokens.User), cookieOptions);
        }



        private bool IsTokenExpired(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token);

                // Consider expired if less than 2 minutes remaining
                return jwtToken.ValidTo < DateTime.Now.AddMinutes(2);
            }
            catch
            {
                return true;
            }
        }


        private string? GetCurrentToken()
        {
            var context = _httpContextAccessor.HttpContext;
            return context?.Request.Cookies["jwt_token"]
                ?? context?.Session.GetString("JwtToken");
        }


        public async Task ClearAllTokensAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Clear session
            context.Session.Clear();

            await Task.Delay(1);

            // Clear cookies
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            };

            context.Response.Cookies.Delete("jwt_token", cookieOptions);
            context.Response.Cookies.Delete("refresh_token", cookieOptions);
            context.Response.Cookies.Delete("user_data", cookieOptions);
        }


        // Legacy compatibility methods
        public void StoreTokens(TokenDto tokens) => StoreTokensSimple(tokens);
        public void ClearSession() => ClearAllTokensAsync().GetAwaiter().GetResult();

    }
}