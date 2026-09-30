using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.Interfaces;
using ProductService.Application.Services;
using SharedServices.Contracts;

namespace ProductService.Application
{
    public static class DependencyInjection
    {
        /// <remarks>MediatR handlers and validators are registered by the host.</remarks>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IProductManagementService, ProductManagementService>();
            services.AddScoped<IApprovalActionHandler, ProductApprovalActionHandler>();
            return services;
        }
    }
}
