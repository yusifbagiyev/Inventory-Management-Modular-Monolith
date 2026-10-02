using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SharedServices.Contracts;
using SharedServices.LiveUpdates;
using SharedServices.Persistence;

namespace IdentityService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<IdentityDbContext>(IdentityDbContext.Schema);
            // Sign-ins touch these columns, and nobody needs to see that live.
            services.TrackLiveEntity<User>("user", "Id",
                nameof(User.LastLoginAt), nameof(User.SecurityStamp), nameof(User.ConcurrencyStamp),
                nameof(User.AccessFailedCount), nameof(User.LockoutEnd));
            // Role and permission grants change what the user row shows.
            services.TrackLiveEntity<IdentityUserRole<int>>("user", "UserId");
            services.TrackLiveEntity<UserPermission>("user", "UserId");

            // Not AddIdentity, which would register its own cookie scheme over the host's schemes.
            services.AddIdentityCore<User>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = SharedServices.Identity.PasswordRules.MinLength;
                    options.Password.RequireNonAlphanumeric = false;
                    options.User.RequireUniqueEmail = true;

                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    // Not lower, since anyone can lock someone else out with wrong guesses.
                    options.Lockout.MaxFailedAccessAttempts = 10;
                    options.Lockout.AllowedForNewUsers = true;

                    options.SignIn.RequireConfirmedAccount = false;
                    options.SignIn.RequireConfirmedEmail = false;
                })
                .AddRoles<Role>()
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserDirectory, UserDirectory>();

            return services;
        }
    }
}
