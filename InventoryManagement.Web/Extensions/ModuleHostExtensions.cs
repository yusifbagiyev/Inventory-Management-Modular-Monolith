using System.Reflection;
using System.Threading.RateLimiting;
using ApprovalService.API;
using AuditService;
using AuditService.Data;
using IdentityService.API;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using NotificationService.API;
using NotificationService.Infrastructure.Data;
using ProductService.API;
using ProductService.Infrastructure.Data;
using RouteService.API;
using RouteService.Infrastructure.Data;
using ApprovalService.Infrastructure.Data;
using SharedServices;
using SharedServices.Identity;
using SharedServices.Web;

namespace InventoryManagement.Web.Extensions
{
    /// <summary>Composition root that wires the backend modules into this host.</summary>
    public static class ModuleHostExtensions
    {
        private static readonly Assembly[] ModuleAssemblies =
        [
            .. IdentityModule.Assemblies,
            .. ProductModule.Assemblies,
            .. RouteModule.Assemblies,
            .. ApprovalModule.Assemblies,
            .. NotificationModule.Assemblies,
            .. AuditModule.Assemblies
        ];

        // In migration order.
        private static readonly Type[] ModuleDbContexts =
        [
            typeof(IdentityDbContext),
            typeof(ProductDbContext),
            typeof(RouteDbContext),
            typeof(ApprovalDbContext),
            typeof(NotificationDbContext),
            typeof(AuditDbContext)
        ];

        private static readonly string[] ModuleSchemas =
        [
            IdentityDbContext.Schema,
            ProductDbContext.Schema,
            RouteDbContext.Schema,
            ApprovalDbContext.Schema,
            NotificationDbContext.Schema,
            AuditDbContext.Schema
        ];

        public static IServiceCollection AddModules(this IServiceCollection services, IMvcBuilder mvc)
        {
            services.AddSharedKernel(ModuleAssemblies);

            services.AddIdentityModule();
            services.AddProductModule();
            services.AddRouteModule();
            services.AddApprovalModule();
            services.AddNotificationModule();
            services.AddAuditModule();

            foreach (var assembly in ModuleAssemblies.Distinct())
                mvc.AddApplicationPart(assembly);
            mvc.AddMvcOptions(options => options.Conventions.Add(new ModuleApiAreaConvention(ModuleAssemblies)));

            services.AddSignalR(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                // Background tabs throttle timers to about once a minute, so pings can arrive a minute late.
                options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
            });

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                // Coarse brake on sign-in posts per address. It is generous because a whole office shares one public IP.
                // Keyed on RemoteIpAddress, never the raw X-Forwarded-For header, which the client controls.
                options.AddPolicy(IdentityModule.LoginRateLimitPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            });
            services.AddSingleton<LoginThrottle>();

            // Anything not marked [AllowAnonymous] needs a signed-in user, so a forgotten [Authorize] is not a hole.
            services.Configure<AuthorizationOptions>(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Only nginx can reach the app, but its compose address is not fixed. Trust exactly one hop.
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardLimit = 1;
            });

            return services;
        }

        /// <summary>Applies pending migrations for every module, then realigns identity sequences.</summary>
        public static async Task MigrateModulesAsync(this IServiceProvider services)
        {
            await using var scope = services.CreateAsyncScope();
            foreach (var contextType in ModuleDbContexts)
            {
                var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                await context.Database.MigrateAsync();
            }

            // Seeded and copied rows have explicit ids that don't advance the sequences, so the next insert would collide.
            // This only ever moves a sequence forward.
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(ModuleDbContexts[0]);
            var schemas = string.Join(",", ModuleSchemas.Select(s => $"'{s}'"));
            // Only the fixed schema list is interpolated here, never user input.
            var alignSequences = $$"""
                DO $$
                DECLARE
                    r record; seq text; max_id bigint; last bigint; called boolean;
                BEGIN
                    FOR r IN SELECT table_schema, table_name, column_name
                             FROM information_schema.columns
                             WHERE is_identity = 'YES' AND table_schema IN ({{schemas}})
                    LOOP
                        seq := pg_get_serial_sequence(format('%I.%I', r.table_schema, r.table_name), r.column_name);
                        EXECUTE format('SELECT COALESCE(MAX(%I), 0) FROM %I.%I', r.column_name, r.table_schema, r.table_name) INTO max_id;
                        EXECUTE format('SELECT last_value, is_called FROM %s', seq) INTO last, called;
                        IF max_id >= (CASE WHEN called THEN last + 1 ELSE last END) THEN
                            PERFORM setval(seq, max_id + 1, false);
                        END IF;
                    END LOOP;
                END $$;
                """;
            await db.Database.ExecuteSqlRawAsync(alignSequences);
        }

        /// <summary>Adds API error handling, CSRF checks for cookie API calls and the SignalR hub.</summary>
        public static WebApplication UseModules(this WebApplication app)
        {
            app.UseWhen(IsApiRequest, api =>
            {
                api.UseMiddleware<ApiExceptionMiddleware>();
                api.Use(ValidateAntiforgeryForCookieCalls);
            });

            app.MapNotificationHub();
            return app;
        }

        public static bool IsApiRequest(HttpContext context) => context.Request.Path.StartsWithSegments("/api");

        /// <summary>Unsafe API calls made with the cookie need the antiforgery token. Bearer and API key callers are exempt.</summary>
        private static async Task ValidateAntiforgeryForCookieCalls(HttpContext context, Func<Task> next)
        {
            var method = context.Request.Method;
            var safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
            // Ask the scheme selector. Any other Authorization header still falls back to the cookie and must be checked.
            var usesCredentialHeader = AuthenticationExtensions.SelectScheme(context) != CookieAuthenticationDefaults.AuthenticationScheme;

            if (!safe && !usesCredentialHeader && context.User.Identity?.IsAuthenticated == true)
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing antiforgery token" });
                    return;
                }
            }

            await next();
        }
    }

    /// <summary>Puts module API controllers in the Api area so UI links never resolve to a same-named API action.</summary>
    internal sealed class ModuleApiAreaConvention : IControllerModelConvention
    {
        private readonly HashSet<Assembly> _moduleAssemblies;

        public ModuleApiAreaConvention(IEnumerable<Assembly> moduleAssemblies)
        {
            _moduleAssemblies = moduleAssemblies.ToHashSet();
        }

        public void Apply(ControllerModel controller)
        {
            if (_moduleAssemblies.Contains(controller.ControllerType.Assembly))
                controller.RouteValues["area"] = "Api";
        }
    }
}
