using System.IdentityModel.Tokens.Jwt;
using InventoryManagement.Web.Services;
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
                //Check if the user is authenticated via cookie but has no JWT token in session
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var token = context.Session.GetString("JwtToken");
                    var refreshToken = context.Session.GetString("RefreshToken");

                    // If no tokens in session, user might have logged in with saved password
                    if ((string.IsNullOrEmpty(token)&& string.IsNullOrEmpty(refreshToken)))
                    {
                        _logger.LogInformation("Authenticated user missing JWT tokens, redirecting to login");

                        // Sign out and redirect to login
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Session.Clear();
                         
                        if(!context.Request.Path.StartsWithSegments("/Account/Login"))
                        {
                            context.Response.Redirect($"/Account/Login?returnUrl={context.Request.Path}");
                            return;
                        }
                    }

                    else
                    {
                        //Check if token is expired
                        var handler = new JwtSecurityTokenHandler();
                        if (handler.CanReadToken(token))
                        {
                            var jwtToken = handler.ReadJwtToken(token);

                            if (jwtToken.ValidTo < DateTime.UtcNow)
                            {
                                _logger.LogInformation("JWT token expired, attempting to refresh");

                                try
                                {
                                    if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(refreshToken))
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
                                            throw new Exception("Failed to refresh token");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Token refresh failed");
                                    // Sign out and redirect to login
                                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                    context.Session.Clear();

                                    if (!context.Request.Path.StartsWithSegments("/Account/Login"))
                                    {
                                        context.Response.Redirect("/Account/Login");
                                        return;
                                    }
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
    }
}