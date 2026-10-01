using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Auditing;
using SharedServices.Authorization;
using SharedServices.Background;
using SharedServices.Behaviors;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace SharedServices
{
    public static class SharedKernelExtensions
    {
        /// <summary>
        /// Infrastructure shared by every module: the per-request database session, MediatR with
        /// validation + transaction behaviors, validators, background work, image storage and
        /// permission-based authorization.
        /// </summary>
        public static IServiceCollection AddSharedKernel(this IServiceCollection services, params Assembly[] moduleAssemblies)
        {
            services.AddSingleton<BackgroundWorkQueue>();
            services.AddHostedService<BackgroundWorkService>();
            services.AddScoped<DbSession>();
            services.AddHttpContextAccessor();
            services.AddScoped<AuditContext>();
            services.AddSingleton<ImageStorage>();

            services.AddMediatR(config =>
            {
                config.RegisterServicesFromAssemblies(moduleAssemblies);
                // Registration order = execution order: name the audited action, validate, then
                // open a transaction.
                config.AddOpenBehavior(typeof(AuditActionBehavior<,>));
                config.AddOpenBehavior(typeof(ValidationBehavior<,>));
                config.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });
            services.AddValidatorsFromAssemblies(moduleAssemblies);

            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

            return services;
        }
    }
}
