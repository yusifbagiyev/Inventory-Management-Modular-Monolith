using System.Security.Claims;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            // Add HTTP clients
            services.AddHttpClient<IApiService, ApiService>();
            services.AddHttpClient<IAuthService, AuthService>();
            services.AddHttpClient<IApprovalService, Services.ApprovalService>();
            services.AddHttpClient<INotificationService, Services.NotificationService>();
            services.AddHttpClient<IUserManagementService, UserManagementService>();

            // Add other services. NOTE: the five interfaces above are registered by AddHttpClient
            // (typed clients with a pooled handler); re-registering them with AddScoped here used
            // to override that and throw away connection pooling, so only the rest are listed.
            services.AddScoped<IUrlService, UrlService>();
            services.AddScoped<ITokenManager, TokenManager>();
            services.AddScoped<IWordExportService, WordExportService>();

            return services;
        }

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