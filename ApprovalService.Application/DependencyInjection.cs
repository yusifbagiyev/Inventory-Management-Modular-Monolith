using ApprovalService.Application.Interfaces;
using ApprovalService.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Contracts;

namespace ApprovalService.Application
{
    public static class DependencyInjection
    {
        /// <remarks>MediatR handlers are registered by the host.</remarks>
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IActionExecutor, ActionExecutor>();
            services.AddScoped<IApprovalRequests, ApprovalRequests>();
            return services;
        }
    }
}
