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

        public async Task InvokeAsync(HttpContext context,IAuthService authService)
        {
            try
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var token = context.Session.GetString("JwtToken");
                    var refreshToken = context.Session.GetString("RefreshToken");

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
                                        context.Session.SetString("JwtToken", result.AccessToken);
                                        context.Session.SetString("RefreshToken", result.RefreshToken);
                                        context.Session.SetString("UserData", JsonConvert.SerializeObject(result.User));
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
            if (!context.Request.Path.StartsWithSegments("/Account/Login"))
            {
                context.Response.Redirect($"/Account/Login?returnUrl={context.Request.Path}");
            }
        }
    }
}