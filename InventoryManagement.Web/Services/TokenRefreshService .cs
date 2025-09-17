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

        public TokenManager(
            IAuthService authService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TokenManager> logger)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            if (!await _refreshSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Token refresh already in progress");
                return false;
            }

            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return false;

                var accessToken = context.Session.GetString("JwtToken");
                var refreshToken = context.Session.GetString("RefreshToken");

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogWarning("No tokens available for refresh");
                    return false;
                }

                // Call auth service to refresh
                var newTokens = await _authService.RefreshTokenAsync(accessToken, refreshToken);
                if (newTokens == null)
                {
                    _logger.LogWarning("Token refresh failed - clearing session");
                    ClearSession();
                    return false;
                }

                // Update session
                StoreTokens(newTokens);
                _logger.LogInformation("Token refreshed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                ClearSession();
                return false;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        public void StoreTokens(TokenDto tokens)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            // Store in session only
            context.Session.SetString("JwtToken", tokens.AccessToken);
            context.Session.SetString("RefreshToken", tokens.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokens.User));
            context.Session.SetString("TokenExpiry", tokens.ExpiresAt.ToString("O"));
        }

        public void ClearSession()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            context.Session.Clear();

            // Clear cookies if they exist
            if (context.Request.Cookies.ContainsKey("jwt_token"))
            {
                context.Response.Cookies.Delete("jwt_token");
                context.Response.Cookies.Delete("refresh_token");
                context.Response.Cookies.Delete("user_data");
            }
        }

        private bool IsTokenExpiringSoon(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var timeUntilExpiry = jwtToken.ValidTo - DateTime.UtcNow;

                // Refresh if less than 5 minutes remaining
                return timeUntilExpiry.TotalMinutes <= 5;
            }
            catch
            {
                return true; // Assume expired if can't parse
            }
        }

        public async Task<string?> GetValidTokenAsync()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // Get current token from session only
            var token = context.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token)) return null;

            // Check if token needs refresh
            if (IsTokenExpiringSoon(token))
            {
                await RefreshTokenAsync();
                token = context.Session.GetString("JwtToken");
            }

            return token;
        }
    }
}
