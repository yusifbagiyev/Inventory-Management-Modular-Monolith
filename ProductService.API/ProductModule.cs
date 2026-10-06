using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Application;
using ProductService.Infrastructure;

namespace ProductService.API
{
    /// <summary>Products, categories and departments.</summary>
    public static class ProductModule
    {
        /// <summary>Assemblies holding this module's controllers, handlers, validators and mappings.</summary>
        public static Assembly[] Assemblies =>
        [
            typeof(ProductModule).Assembly,
            typeof(Application.DependencyInjection).Assembly
        ];

        public static IServiceCollection AddProductModule(this IServiceCollection services)
        {
            services.AddApplication();
            services.AddInfrastructure();
            return services;
        }
    }
}
