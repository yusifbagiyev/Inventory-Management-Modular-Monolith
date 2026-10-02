using System.Reflection;
using AuditService.Data;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Auditing;
using SharedServices.Persistence;

namespace AuditService
{
    /// <summary>The audit log of every saved change and sign-in event.</summary>
    public static class AuditModule
    {
        public static Assembly[] Assemblies => [typeof(AuditModule).Assembly];

        public static IServiceCollection AddAuditModule(this IServiceCollection services)
        {
            // Otherwise the audit rows would be audited too.
            services.AddModuleDbContext<AuditDbContext>(AuditDbContext.Schema, audited: false);
            services.AddScoped<IAuditSink, AuditSink>();
            return services;
        }
    }
}
