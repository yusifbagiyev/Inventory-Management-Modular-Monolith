using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using InventoryManagement.Web.Services;
using ProductService.API.Authentication;

namespace InventoryManagement.Web.Extensions
{
    public static class AuthenticationExtensions
    {
        /// <summary>Selects cookie, JWT or API-key authentication per request.</summary>
        public const string DefaultScheme = "CookieOrToken";
        public const string ApiKeyScheme = "ApiKey";
        public const string ApiKeyHeader = "X-Api-Key";

        /// <summary>
        /// The UI signs in with a cookie. /api additionally accepts a JWT bearer token (issued by
        /// /api/auth/login) and, for internal integrations such as ServiceDesk, an X-Api-Key.
        /// </summary>
        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = DefaultScheme;
                options.DefaultChallengeScheme = DefaultScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(DefaultScheme, DefaultScheme, options => options.ForwardDefaultSelector = SelectScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue("Authentication:CookieExpirationMinutes", 480));
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // TLS terminates at nginx; with forwarded headers applied the request is HTTPS.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                options.Events.OnValidatePrincipal = UserPrincipalFactory.RefreshAsync;

                // AJAX and /api callers get a status code they can act on instead of a redirect
                // to the login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    if (WantsStatusCode(context.Request))
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    else
                        context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (WantsStatusCode(context.Request))
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    else
                        context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                        configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured"))),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                // A token carries the user's permissions as they were when it was issued; it is
                // refused once the user is deactivated or their password changes (session stamp),
                // checked at most once a minute per user.
                options.Events = new JwtBearerEvents { OnTokenValidated = ValidateTokenUserAsync };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, null);

            return services;
        }

        /// <summary>
        /// The scheme for a request. Bearer tokens and API keys only count on /api: pages use the
        /// cookie alone. The API key (ServiceDesk, product.view) only on the product API it was
        /// made for, which it reaches on the LAN port 5001; nginx also drops the header on the
        /// public and LAN entries.
        /// </summary>
        public static string SelectScheme(HttpContext context)
        {
            if (!ModuleHostExtensions.IsApiRequest(context))
                return CookieAuthenticationDefaults.AuthenticationScheme;

            if (context.Request.Headers.ContainsKey(ApiKeyHeader))
                return IsApiKeyPath(context.Request.Path) ? ApiKeyScheme : CookieAuthenticationDefaults.AuthenticationScheme;

            var authorization = context.Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;

            return CookieAuthenticationDefaults.AuthenticationScheme;
        }

        private static bool IsApiKeyPath(PathString path)
            => path.StartsWithSegments("/api/products", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/categories", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/departments", StringComparison.OrdinalIgnoreCase);

        private static async Task ValidateTokenUserAsync(TokenValidatedContext context)
        {
            var principal = context.Principal;
            if (!int.TryParse(principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                context.Fail("Token has no user");
                return;
            }

            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var current = await cache.GetOrCreateAsync($"session-stamp:{userId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return await context.HttpContext.RequestServices
                    .GetRequiredService<IdentityService.Application.Services.IAuthService>()
                    .GetSessionStampAsync(userId);
            });

            if (current == null || principal!.FindFirst(SessionStampClaim)?.Value != current)
                context.Fail("The user's session has ended");
        }

        /// <summary>Claim holding the session stamp in cookies and tokens.</summary>
        public const string SessionStampClaim = "SessionStamp";

        private static bool WantsStatusCode(HttpRequest request)
            => ModuleHostExtensions.IsApiRequest(request.HttpContext)
               || request.Headers.XRequestedWith == "XMLHttpRequest"
               || request.Headers.Accept.ToString().Contains("application/json");
    }
}
