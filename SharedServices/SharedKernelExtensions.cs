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
        /// <summary>Registers the infrastructure every module shares.</summary>
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
                // Behaviors run in registration order. Validation must happen before the transaction opens.
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
