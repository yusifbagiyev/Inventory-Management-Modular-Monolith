using System.IdentityModel.Tokens.Jwt;
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

        public JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            try
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    string? token = null;
                    string? refreshToken = null;

                    // Try to get tokens from session first (always available for logged-in users)
                    token = context.Session.GetString("JwtToken");
                    refreshToken = context.Session.GetString("RefreshToken");

                    // If not in session, try cookies (for Remember Me scenarios after session expires)
                    if (string.IsNullOrEmpty(token))
                    {
                        token = context.Request.Cookies["jwt_token"];
                        refreshToken = context.Request.Cookies["refresh_token"];

                        // If found in cookies, restore to session for this request
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Session.SetString("JwtToken", token);
                            context.Session.SetString("RefreshToken", refreshToken ?? "");

                            // Also restore user data if available
                            var userData = context.Request.Cookies["user_data"];
                            if (!string.IsNullOrEmpty(userData))
                            {
                                context.Session.SetString("UserData", userData);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogInformation("Authenticated user missing JWT tokens, redirecting to login");
                        await SignOutAndRedirect(context);
                        return;
                    }

                    // Store token in HttpContext.Items for this request
                    context.Items["JwtToken"] = token;
                    context.Items["RefreshToken"] = refreshToken;

                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);

                        // Check if token is expired or expiring soon
                        if (jwtToken.ValidTo < DateTime.UtcNow.AddHours(4).AddMinutes(5))
                        {
                            _logger.LogInformation("JWT token expiring soon, attempting to refresh");

                            if (!string.IsNullOrEmpty(refreshToken))
                            {
                                try
                                {
                                    var result = await authService.RefreshTokenAsync(token, refreshToken);
                                    if (result != null)
                                    {
                                        // Update tokens everywhere
                                        context.Items["JwtToken"] = result.AccessToken;
                                        context.Items["RefreshToken"] = result.RefreshToken;

                                        // Update session
                                        context.Session.SetString("JwtToken", result.AccessToken);
                                        context.Session.SetString("RefreshToken", result.RefreshToken);
                                        context.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));

                                        // Update cookies if they exist (Remember Me was used)
                                        if (context.Request.Cookies.ContainsKey("jwt_token"))
                                        {
                                            var cookieOptions = new CookieOptions
                                            {
                                                HttpOnly = true,
                                                Secure = context.Request.IsHttps,
                                                SameSite = SameSiteMode.Lax,
                                                Expires = DateTimeOffset.UtcNow.AddDays(30)
                                            };

                                            context.Response.Cookies.Append("jwt_token", result.AccessToken, cookieOptions);
                                            context.Response.Cookies.Append("refresh_token", result.RefreshToken, cookieOptions);
                                            context.Response.Cookies.Append("user_data", JsonConvert.SerializeObject(result.User), cookieOptions);
                                        }

                                        _logger.LogInformation("Token refreshed successfully");
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Token refresh returned null");
                                        await SignOutAndRedirect(context);
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Token refresh failed");
                                    await SignOutAndRedirect(context);
                                    return;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("No refresh token available");
                                await SignOutAndRedirect(context);
                                return;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Cannot read JWT token");
                        await SignOutAndRedirect(context);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT middleware");
            }

            await _next(context);
        }

        private async Task SignOutAndRedirect(HttpContext context)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Session.Clear();

            // Clear cookies if they exist
            var cookiesToClear = new[] { "jwt_token", "refresh_token", "user_data" };
            foreach (var cookieName in cookiesToClear)
            {
                if (context.Request.Cookies.ContainsKey(cookieName))
                {
                    context.Response.Cookies.Delete(cookieName);
                }
            }

            if (!context.Request.Path.StartsWithSegments("/Account/Login"))
            {
                context.Response.Redirect($"/Account/Login?returnUrl={context.Request.Path}");
            }
        }
    }
}