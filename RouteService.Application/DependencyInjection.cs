using Microsoft.Extensions.DependencyInjection;
using RouteService.Application.Interfaces;
using RouteService.Application.Services;
using SharedServices.Contracts;

namespace RouteService.Application
{
    public static class DependencyInjection
    {
        /// <remarks>MediatR handlers, validators and AutoMapper profiles are registered by the host.</remarks>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IRouteManagementService, RouteManagementService>();
            services.AddScoped<IApprovalActionHandler, RouteApprovalActionHandler>();
            return services;
        }
    }
}
