using System.Reflection;
using AuditService.Data;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Auditing;
using SharedServices.Persistence;

namespace AuditService
{
    /// <summary>
    /// The audit log: every change any module saves (SharedServices.Auditing.AuditInterceptor) and
    /// sign-in events, kept in the "audit" schema and shown on the Audit page (audit.view).
    /// </summary>
    public static class AuditModule
    {
        public static Assembly[] Assemblies => [typeof(AuditModule).Assembly];

        public static IServiceCollection AddAuditModule(this IServiceCollection services)
        {
            // Not audited itself (that would audit the audit rows).
            services.AddModuleDbContext<AuditDbContext>(AuditDbContext.Schema, audited: false);
            services.AddScoped<IAuditSink, AuditSink>();
            return services;
        }
    }
}
