using ApprovalService.Domain.Repositories;
using ApprovalService.Infrastructure.Data;
using ApprovalService.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Persistence;

namespace ApprovalService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<ApprovalDbContext>(ApprovalDbContext.Schema);

            services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
