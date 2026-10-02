using Microsoft.Extensions.DependencyInjection;
using RouteService.Application.Interfaces;
using RouteService.Application.Services;
using SharedServices.Contracts;

namespace RouteService.Application
{
    public static class DependencyInjection
    {
        /// <summary>Registers the module's services, while the host registers its MediatR handlers and validators.</summary>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IRouteManagementService, RouteManagementService>();
            services.AddScoped<IApprovalActionHandler, RouteApprovalActionHandler>();
            return services;
        }
    }
}
