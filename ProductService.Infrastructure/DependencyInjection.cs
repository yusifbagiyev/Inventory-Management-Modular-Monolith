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

            // Configure Redis Caching (Optional - Toggle via appsettings.json)
            var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
            if (redisEnabled)
            {
                var redisConnectionString = configuration.GetValue<string>("Redis:ConnectionString");
                if (!string.IsNullOrEmpty(redisConnectionString))
                {
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = redisConnectionString;
                        options.InstanceName = "ProductService_";
                    });
                    services.AddScoped<ICacheService, RedisCacheService>();
                    Console.WriteLine("Redis caching is ENABLED for ProductService");
                }
                else
                {
                    services.AddScoped<ICacheService, NoCacheService>();
                    Console.WriteLine("Redis enabled but connection string missing. Using NoCacheService.");
                }
            }
            else
            {
                services.AddScoped<ICacheService, NoCacheService>();
                Console.WriteLine("Redis caching is DISABLED for ProductService");
            }

            return services;
        }
    }
}