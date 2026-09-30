using Microsoft.Extensions.DependencyInjection;
using RouteService.Domain.Repositories;
using RouteService.Infrastructure.Data;
using RouteService.Infrastructure.Repositories;
using RouteService.Domain.Entities;
using SharedServices.LiveUpdates;
using SharedServices.Persistence;

namespace RouteService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<RouteDbContext>(RouteDbContext.Schema);
            services.TrackLiveEntity<InventoryRoute>("route");

            services.AddScoped<IInventoryRouteRepository, InventoryRouteRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
