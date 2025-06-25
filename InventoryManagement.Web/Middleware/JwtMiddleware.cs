using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

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

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var token = context.Session.GetString("JwtToken");

                if (!string.IsNullOrEmpty(token))
                {
                    // Check if token is expired
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);

                    if (jwtToken.ValidTo < DateTime.UtcNow)
                    {
                        _logger.LogInformation("JWT token expired, attempting refresh");

                        // Token is expired, try to refresh
                        var refreshToken = context.Session.GetString("RefreshToken");

                        if (!string.IsNullOrEmpty(refreshToken))
                        {
                            // Call the refresh endpoint through the AccountController
                            // This would typically be done via the AuthService
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JWT middleware");
            }

            await _next(context);
        }
    }
}