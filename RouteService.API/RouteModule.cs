using System.Reflection;
using RouteService.Application;
using RouteService.Infrastructure;

namespace RouteService.API
{
    /// <summary>Inventory routes: transfers and the product audit trail.</summary>
    public static class RouteModule
    {
        /// <summary>Assemblies holding this module's controllers, handlers, validators and mappings.</summary>
        public static Assembly[] Assemblies =>
        [
            typeof(RouteModule).Assembly,
            typeof(Application.DependencyInjection).Assembly
        ];

        public static IServiceCollection AddRouteModule(this IServiceCollection services)
        {
            services.AddApplication();
            services.AddInfrastructure();
            return services;
        }
    }
}
