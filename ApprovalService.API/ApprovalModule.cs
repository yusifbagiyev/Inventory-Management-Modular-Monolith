using System.Reflection;
using ApprovalService.Application;
using ApprovalService.Infrastructure;

namespace ApprovalService.API
{
    /// <summary>Approval requests for actions the requester may not perform directly.</summary>
    public static class ApprovalModule
    {
        /// <summary>Assemblies holding this module's controllers, handlers, validators and mappings.</summary>
        public static Assembly[] Assemblies =>
        [
            typeof(ApprovalModule).Assembly,
            typeof(Application.DependencyInjection).Assembly
        ];

        public static IServiceCollection AddApprovalModule(this IServiceCollection services)
        {
            services.AddApplication();
            services.AddInfrastructure();
            return services;
        }
    }
}
