using ApprovalService.Domain.Repositories;
using ApprovalService.Infrastructure.Data;
using ApprovalService.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using ApprovalService.Domain.Entities;
using SharedServices.LiveUpdates;
using SharedServices.Persistence;

namespace ApprovalService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<ApprovalDbContext>(ApprovalDbContext.Schema);
            services.TrackLiveEntity<ApprovalRequest>("approval");

            services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
