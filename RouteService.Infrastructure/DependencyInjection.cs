using Microsoft.Extensions.DependencyInjection;
using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;
using RouteService.Infrastructure.Repositories;
using SharedServices.Persistence;

namespace RouteService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<RouteDbContext>(RouteDbContext.Schema);

            services.AddScoped<IInventoryRouteRepository, InventoryRouteRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
