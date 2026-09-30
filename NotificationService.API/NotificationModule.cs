using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NotificationService.Application.Services;
using NotificationService.Infrastructure;

namespace NotificationService.API
{
    /// <summary>In-app notifications (stored + SignalR) and WhatsApp group messages.</summary>
    public static class NotificationModule
    {
        public const string HubPath = "/notificationHub";

        /// <summary>Assemblies holding this module's controllers and event handlers.</summary>
        public static Assembly[] Assemblies =>
        [
            typeof(NotificationModule).Assembly,
            typeof(NotificationHub).Assembly
        ];

        public static IServiceCollection AddNotificationModule(this IServiceCollection services)
        {
            services.AddInfrastructure();
            return services;
        }

        public static IEndpointRouteBuilder MapNotificationHub(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHub<NotificationHub>(HubPath).RequireAuthorization();
            return endpoints;
        }
    }
}
