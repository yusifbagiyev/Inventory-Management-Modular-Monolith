using System.Reflection;
using IdentityService.Infrastructure;

namespace IdentityService.API
{
    /// <summary>Users, roles, permissions and JWT issuing for API clients.</summary>
    public static class IdentityModule
    {
        /// <summary>Name of the per-IP rate-limit policy the host must register for the login endpoints.</summary>
        public const string LoginRateLimitPolicy = "LoginPolicyPerIP";

        public static Assembly[] Assemblies => [typeof(IdentityModule).Assembly];

        public static IServiceCollection AddIdentityModule(this IServiceCollection services)
        {
            services.AddInfrastructure();
            return services;
        }
    }
}
