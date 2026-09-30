using Microsoft.Extensions.DependencyInjection;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using ProductService.Infrastructure.Repositories;
using ProductService.Infrastructure.Services;
using SharedServices.Contracts;
using ProductService.Domain.Entities;
using SharedServices.LiveUpdates;
using SharedServices.Persistence;

namespace ProductService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<ProductDbContext>(ProductDbContext.Schema);
            services.TrackLiveEntity<Product>("product");
            services.TrackLiveEntity<Category>("category");
            services.TrackLiveEntity<Department>("department");

            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<ProductCatalog>();
            services.AddScoped<IProductCatalog>(sp => sp.GetRequiredService<ProductCatalog>());
            services.AddScoped<IProductTransfers>(sp => sp.GetRequiredService<ProductCatalog>());

            return services;
        }
    }
}
