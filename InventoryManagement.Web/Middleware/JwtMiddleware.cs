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
                    bool isRemembered = false;

                    // Check if user selected "Remember Me"
                    var rememberMeClaim = context.User.FindFirst("RememberMe")?.Value;
                    if (rememberMeClaim == "True")
                    {
                        // First check cookies for persistent login
                        token = context.Request.Cookies["jwt_token"];
                        refreshToken = context.Request.Cookies["refresh_token"];
                        isRemembered = true;
                    }

                    // Fall back to session if not in cookies
                    if (string.IsNullOrEmpty(token))
                    {
                        token = context.Session.GetString("JwtToken");
                        refreshToken = context.Session.GetString("RefreshToken");
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        _logger.LogInformation("Authenticated user missing JWT tokens, redirecting to login");
                        await SignOutAndRedirect(context);
                        return;
                    }

                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);

                        // Check if token is expired or expiring soon
                        if (jwtToken.ValidTo < DateTime.UtcNow.AddMinutes(5))
                        {
                            _logger.LogInformation("JWT token expiring soon, attempting to refresh");

                            if (!string.IsNullOrEmpty(refreshToken))
                            {
                                try
                                {
                                    var result = await authService.RefreshTokenAsync(token, refreshToken);
                                    if (result != null)
                                    {
                                        // Update storage based on remember me preference
                                        if (isRemembered)
                                        {
                                            var cookieOptions = new CookieOptions
                                            {
                                                HttpOnly = true,
                                                Secure = true,
                                                SameSite = SameSiteMode.Strict,
                                                Expires = DateTimeOffset.UtcNow.AddDays(30)
                                            };

                                            context.Response.Cookies.Append("jwt_token", result.AccessToken, cookieOptions);
                                            context.Response.Cookies.Append("refresh_token", result.RefreshToken, cookieOptions);
                                            context.Response.Cookies.Append("user_data", JsonConvert.SerializeObject(result.User), cookieOptions);
                                        }
                                        else
                                        {
                                            context.Session.SetString("JwtToken", result.AccessToken);
                                            context.Session.SetString("RefreshToken", result.RefreshToken);
                                            context.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));
                                        }

                                        _logger.LogInformation("Token refreshed successfully");
                                    }
                                    else
                                    {
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
                        }
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

            // Clear cookies
            context.Response.Cookies.Delete("jwt_token");
            context.Response.Cookies.Delete("refresh_token");
            context.Response.Cookies.Delete("user_data");

            if (!context.Request.Path.StartsWithSegments("/Account/Login"))
            {
                context.Response.Redirect($"/Account/Login?returnUrl={context.Request.Path}");
            }
        }
    }
}