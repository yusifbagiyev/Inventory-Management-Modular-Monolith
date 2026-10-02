using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.Interfaces;
using ProductService.Application.Services;
using SharedServices.Contracts;

namespace ProductService.Application
{
    public static class DependencyInjection
    {
        /// <summary>Registers the module's services, while the host registers its MediatR handlers and validators.</summary>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IProductManagementService, ProductManagementService>();
            services.AddScoped<IApprovalActionHandler, ProductApprovalActionHandler>();
            return services;
        }
    }
}
