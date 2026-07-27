using System.Security.Claims;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using NotificationService.Application.Services;

namespace InventoryManagement.Web.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            // Add HTTP clients
            services.AddHttpClient<IApiService, ApiService>();
            services.AddHttpClient<IAuthService, AuthService>();
            services.AddHttpClient<IApprovalService, ApprovalService>();
            services.AddHttpClient<INotificationService, Services.NotificationService>();
            services.AddHttpClient<IUserManagementService, UserManagementService>();

            // Add other services. NOTE: the five interfaces above are registered by AddHttpClient
            // (typed clients with a pooled handler); re-registering them with AddScoped here used
            // to override that and throw away connection pooling, so only the rest are listed.
            services.AddScoped<IUrlService, UrlService>();
            services.AddScoped<ITokenManager, TokenManager>();
            services.AddScoped<IWordExportService, WordExportService>();

            services.AddSingleton<IConnectionManager, ConnectionManager>();
            return services;
        }

        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(configuration.GetValue<int>("Authentication:CookieExpirationMinutes", 480));
                options.SlidingExpiration = true;

                // These handlers used to live in ConfigureApplicationCookie in Program.cs, which
                // configures the ASP.NET Core *Identity* cookie scheme - a scheme this app never
                // registers, so they never ran and AJAX calls got a 302 to the login page instead
                // of a 401 the client could act on.
                options.Events.OnRedirectToLogin = context =>
                {
                    if (IsAjax(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (IsAjax(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

            return services;
        }

        private static bool IsAjax(HttpRequest request)
            => request.Headers["X-Requested-With"] == "XMLHttpRequest"
               || request.Headers.Accept.ToString().Contains("application/json");

        public static bool HasPermission(this ClaimsPrincipal user, string permission)
        {
            return user.Claims.Any(c => c.Type == "permission" && c.Value == permission);
        }

        public static string ToTitleCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var words = str.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }
    }
}