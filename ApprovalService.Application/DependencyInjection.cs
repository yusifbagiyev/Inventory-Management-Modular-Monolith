using ApprovalService.Application.Interfaces;
using ApprovalService.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Contracts;

namespace ApprovalService.Application
{
    public static class DependencyInjection
    {
        /// <summary>Registers the module's services, while its MediatR handlers are registered by the host.</summary>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IActionExecutor, ActionExecutor>();
            services.AddScoped<IApprovalRequests, ApprovalRequests>();
            return services;
        }
    }
}
