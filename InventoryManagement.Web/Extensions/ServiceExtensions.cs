using System.Security.Claims;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>UI-side services; all backed in-process by the modules.</summary>
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddScoped<IApprovalService, Services.ApprovalService>();
            services.AddScoped<INotificationService, Services.NotificationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IWordExportService, WordExportService>();
            services.AddHostedService<StartupWarmup>();
            services.AddHostedService<PhotoMetadataCleanup>();
            services.AddMemoryCache();
            services.AddScoped<RailCounts>();
            services.AddSingleton<ImageThumbnails>();
            return services;
        }

        /// <summary>Same rule as the backend's PermissionHandler: Admins hold every permission.</summary>
        public static bool HasPermission(this ClaimsPrincipal user, string permission)
            => user.IsInRole(SharedServices.Identity.AllRoles.Admin)
               || user.Claims.Any(c => c.Type == UserPrincipalFactory.PermissionClaim && c.Value == permission);
    }
}