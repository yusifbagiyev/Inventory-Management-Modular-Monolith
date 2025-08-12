using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public JwtMiddleware(
            RequestDelegate next, 
            ILogger<JwtMiddleware> logger, 
            IWebHostEnvironment webHostEnvironment)
        {
            _next = next;
            _logger = logger;
            _environment = webHostEnvironment;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            try
            {
                // Skip processing for static files and non-authenticated requests
                if (IsStaticFileRequest(context) || !context.User.Identity?.IsAuthenticated == true)
                {
                    await _next(context);
                    return;
                }

                var userId = context.User.Identity?.Name ?? "Unknown";

                // Retrieve tokens with proper fallback chain
                var tokenInfo = GetTokenInfo(context);

                if (string.IsNullOrEmpty(tokenInfo.AccessToken))
                {
                    _logger.LogWarning(
                        "Authenticated user {UserId} missing JWT tokens, signing out",
                        userId);
                    await SignOutAndRedirect(context);
                    return;
                }

                // Store tokens for current request 
                context.Items["JwtToken"] = tokenInfo.AccessToken;
                context.Items["RefreshToken"] = tokenInfo.RefreshToken;

                if (ShouldRefreshToken(tokenInfo.AccessToken))
                {
                    await RefreshTokenIfNeeded(
                       context,
                       authService,
                       tokenInfo.AccessToken,
                       tokenInfo.RefreshToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in JWT middleware for user {UserId}: {Message}",
                    context.User?.Identity?.Name ?? "Unknown",
                    ex.Message);

                // In production, don't expose errors - just continue
                if (!_environment.IsDevelopment())
                {
                    await _next(context);
                    return;
                }
                throw;
            }
            await _next(context);
        }

        private (string AccessToken, string RefreshToken) GetTokenInfo(HttpContext context)
        {
            var accessToken = context.Session.GetString("JwtToken");
            var refreshToken = context.Session.GetString("RefreshToken");

            if(string.IsNullOrEmpty(accessToken))
            {
                accessToken = GetSecureCookieValue(context, "jwt-token");
                refreshToken = GetSecureCookieValue(context, "refresh-token");

                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Session.SetString("JwtToken", accessToken);
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        context.Session.SetString("RefreshToken", refreshToken);
                    }
                    RestoreUserDataFromCookies(context);
                }
            }
            return (accessToken ?? "", refreshToken ?? "");
        }

        private string GetSecureCookieValue(HttpContext context,string cookieName)
        {
            // In Production, only accept cookies over HTTPS
            if(_environment.IsProduction() && !context.Request.IsHttps)
            {
                _logger.LogWarning("Attempted to read secure cookie {CookieName} over HTTP", cookieName);
                return "";
            }
            return context.Request.Cookies[cookieName] ?? "";
        }

        private void RestoreUserDataFromCookies(HttpContext context)
        {
            var userData = GetSecureCookieValue(context, "user-data");
            if(!string.IsNullOrEmpty(userData))
            {
                context.Session.SetString("UserData", userData);
            }
        }


        private bool ShouldRefreshToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(token))
                    return false;

                var jwtToken = handler.ReadJwtToken(token);
                var tokenExpiry = jwtToken.ValidTo;
                var timeUntilExpiry = tokenExpiry - DateTime.Now;

                return timeUntilExpiry.TotalMinutes < 5;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking token expiration");
                return true;
            }
        }

        private async Task RefreshTokenIfNeeded(
            HttpContext context,
            IAuthService authService,
            string accessToken,
            string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("Cannot refresh token - missing refresh token");
                await SignOutAndRedirect(context);
                return;
            }
            try
            {
                var result = await authService.RefreshTokenAsync(accessToken, refreshToken);
                if (result != null)
                {
                    UpdateTokensEverywhere(context, result);
                    _logger.LogInformation("Token refreshed successfully for user {UserId}",
                        context.User.Identity?.Name);
                }
                else
                {
                    _logger.LogWarning("Token refresh failed - signing out user");
                    await SignOutAndRedirect(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                await SignOutAndRedirect(context);
            }
        }


        private void UpdateTokensEverywhere(HttpContext context, TokenDto tokenDto)
        {
            // Update in current request context
            context.Items["JwtToken"] = tokenDto.AccessToken;
            context.Items["RefreshToken"] = tokenDto.RefreshToken;

            // Update session
            context.Session.SetString("JwtToken", tokenDto.AccessToken);
            context.Session.SetString("RefreshToken", tokenDto.RefreshToken);
            context.Session.SetString("UserData", JsonConvert.SerializeObject(tokenDto.User));

            // Update cookies if Remember Me was used
            if (context.Request.Cookies.ContainsKey("jwt_token"))
            {
                var cookieOptions = CreateSecureCookieOptions(context);

                context.Response.Cookies.Append("jwt_token", tokenDto.AccessToken, cookieOptions);
                context.Response.Cookies.Append("refresh_token", tokenDto.RefreshToken, cookieOptions);
                context.Response.Cookies.Append("user_data",
                    JsonConvert.SerializeObject(tokenDto.User), cookieOptions);
            }
        }

        private CookieOptions CreateSecureCookieOptions(HttpContext context)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = _environment.IsProduction() || context.Request.IsHttps,
                SameSite = _environment.IsProduction() ? SameSiteMode.Strict : SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddDays(30),
                Domain = _environment.IsProduction() ? ".yourdomain.com" : null
            };
        }

        private bool IsStaticFileRequest(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            return path.Contains(".css") || path.Contains(".js") ||
                   path.Contains(".png") || path.Contains(".jpg") ||
                   path.Contains(".ico") || path.Contains(".woff");
        }

        private async Task SignOutAndRedirect(HttpContext context)
        {
            var userId = context.User?.Identity?.Name ?? "Unknown";
            var requestPath = context.Request.Path.Value ?? "Unknown";

            _logger.LogInformation("Signing out user {UserId} and redirecting from {RequestPath} to login",
                userId, requestPath);

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();

            // Clear cookies if they exist
            var cookiesToClear = new[] { "jwt_token", "refresh_token", "user_data" };
            foreach (var cookieName in cookiesToClear)
            {
                if (context.Request.Cookies.ContainsKey(cookieName))
                {
                    context.Response.Cookies.Delete(cookieName);
                    _logger.LogDebug("Cleared authentication cookie {CookieName} for user {UserId}",
                        cookieName, userId);
                }
            }

            if (!context.Request.Path.StartsWithSegments("/Account/Login"))
            {
                var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
                _logger.LogInformation("Redirecting user {UserId} to login with return URL {ReturnUrl}",
                    userId, returnUrl);
                context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
            }
        }
    }
}