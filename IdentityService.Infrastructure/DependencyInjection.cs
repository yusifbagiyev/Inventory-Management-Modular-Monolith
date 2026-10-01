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
            // Sign-ins stamp these; they are not changes anyone needs to see.
            services.TrackLiveEntity<User>("user", "Id",
                nameof(User.LastLoginAt), nameof(User.SecurityStamp), nameof(User.ConcurrencyStamp),
                nameof(User.AccessFailedCount), nameof(User.LockoutEnd));
            // Role and permission grants change what the user row shows.
            services.TrackLiveEntity<IdentityUserRole<int>>("user", "UserId");
            services.TrackLiveEntity<UserPermission>("user", "UserId");

            // AddIdentityCore rather than AddIdentity: the host owns the authentication schemes
            // (cookie for the UI, JWT/API key for /api); AddIdentity would register its own
            // cookie scheme and take over the defaults.
            services.AddIdentityCore<User>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequiredLength = SharedServices.Identity.PasswordRules.MinLength;
                    options.Password.RequireNonAlphanumeric = false;
                    options.User.RequireUniqueEmail = true;

                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    // Not lower: anyone can make wrong guesses at someone else's account.
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
