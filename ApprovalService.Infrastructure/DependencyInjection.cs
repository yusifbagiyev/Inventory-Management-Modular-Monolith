using ApprovalService.Application.Interfaces;
using ApprovalService.Domain.Repositories;
using ApprovalService.Infrastructure.Data;
using ApprovalService.Infrastructure.Repositories;
using ApprovalService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApprovalService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApprovalDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();
            services.AddHttpClient<IActionExecutor, ActionExecutor>()
                    .ConfigureHttpClient(client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

            return services;
        }
    }
}