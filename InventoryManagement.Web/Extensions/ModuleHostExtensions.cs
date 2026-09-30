using System.Reflection;
using System.Threading.RateLimiting;
using ApprovalService.API;
using IdentityService.API;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Antiforgery;
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
using SharedServices.Web;

namespace InventoryManagement.Web.Extensions
{
    /// <summary>Wires the backend modules into this host (the modular monolith's composition root).</summary>
    public static class ModuleHostExtensions
    {
        private static readonly Assembly[] ModuleAssemblies =
        [
            .. IdentityModule.Assemblies,
            .. ProductModule.Assemblies,
            .. RouteModule.Assemblies,
            .. ApprovalModule.Assemblies,
            .. NotificationModule.Assemblies
        ];

        /// <summary>Module DbContexts in migration order.</summary>
        private static readonly Type[] ModuleDbContexts =
        [
            typeof(IdentityDbContext),
            typeof(ProductDbContext),
            typeof(RouteDbContext),
            typeof(ApprovalDbContext),
            typeof(NotificationDbContext)
        ];

        private static readonly string[] ModuleSchemas =
        [
            IdentityDbContext.Schema,
            ProductDbContext.Schema,
            RouteDbContext.Schema,
            ApprovalDbContext.Schema,
            NotificationDbContext.Schema
        ];

        public static IServiceCollection AddModules(this IServiceCollection services, IMvcBuilder mvc)
        {
            services.AddSharedKernel(ModuleAssemblies);

            services.AddIdentityModule();
            services.AddProductModule();
            services.AddRouteModule();
            services.AddApprovalModule();
            services.AddNotificationModule();

            foreach (var assembly in ModuleAssemblies.Distinct())
                mvc.AddApplicationPart(assembly);
            mvc.AddMvcOptions(options => options.Conventions.Add(new ModuleApiAreaConvention(ModuleAssemblies)));

            services.AddSignalR(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            });

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                // Keyed on RemoteIpAddress, which UseForwardedHeaders sets from the proxy-appended
                // X-Forwarded-For entry; the header itself is client-controlled.
                options.AddPolicy(IdentityModule.LoginRateLimitPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(10),
                            QueueLimit = 0
                        }));
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // The app is only reachable through nginx on the compose network, whose address is
                // not fixed. Trust one hop: the entry nginx appended, never the client's own.
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

            // Seeded rows (HasData) and copied production data carry explicit ids, which do not
            // advance identity sequences; the first insert would then collide. Only ever raises.
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(ModuleDbContexts[0]);
            var schemas = string.Join(",", ModuleSchemas.Select(s => $"'{s}'"));
            // The only interpolated value is the fixed schema list above - no user input.
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

        /// <summary>/api error handling, CSRF protection for cookie-authenticated API calls, SignalR.</summary>
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

        /// <summary>
        /// Browser calls to /api authenticate with the auth cookie, which the browser also attaches
        /// to cross-site requests; unsafe methods must therefore carry the antiforgery token.
        /// Bearer-token and API-key clients are not exposed to CSRF and are exempt.
        /// </summary>
        private static async Task ValidateAntiforgeryForCookieCalls(HttpContext context, Func<Task> next)
        {
            var method = context.Request.Method;
            var safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
            var usesCredentialHeader = context.Request.Headers.Authorization.Count > 0
                || context.Request.Headers.ContainsKey(AuthenticationExtensions.ApiKeyHeader);

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

    /// <summary>
    /// Puts the modules' API controllers in the "Api" area. Their URLs are attribute routes and do
    /// not change, but the area keeps MVC link generation (asp-action="Create" on the Products
    /// page) from resolving to the same-named API action (/api/Products) instead of the UI one.
    /// </summary>
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
