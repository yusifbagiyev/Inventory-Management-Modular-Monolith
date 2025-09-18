using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.Interfaces;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using ProductService.Infrastructure.Repositories;
using ProductService.Infrastructure.Services;

namespace ProductService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<ProductDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                b=>b.MigrationsAssembly(typeof(ProductDbContext).Assembly.FullName)));

            // Repositories
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            //Services
            services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

            services.AddHttpClient<IApprovalService, ApprovalServiceClient>();
            services.AddHttpContextAccessor();

            // Add RabbitMQ Consumer as hosted service
            services.AddSingleton<RabbitMQConsumer>();
            services.AddHostedService(provider => provider.GetRequiredService<RabbitMQConsumer>());

            return services;
        }
    }
}