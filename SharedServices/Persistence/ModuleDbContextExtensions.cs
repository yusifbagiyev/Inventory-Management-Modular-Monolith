using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedServices.Auditing;
using SharedServices.LiveUpdates;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace SharedServices.Persistence
{
    public static class ModuleDbContextExtensions
    {
        /// <summary>Registers a module DbContext on the shared connection with migrations history in its own schema.</summary>
        public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema, bool audited = true)
            where TContext : DbContext
        {
            services.AddDbContext<TContext>((sp, options) =>
            {
                var session = sp.GetRequiredService<DbSession>();
                options.UseNpgsql(session.Connection, npgsql => ConfigureNpgsql<TContext>(npgsql, schema));
                options.AddInterceptors(new TransactionEnlistmentInterceptor(session));

                var live = sp.GetService<IOptions<LiveUpdateOptions>>()?.Value;
                if (live is { Entities.Count: > 0 })
                    options.AddInterceptors(new LiveUpdateInterceptor(session, live, sp.GetService<IHttpContextAccessor>()));

                // The sink exists only when the Audit module is registered.
                if (audited && sp.GetService<IAuditSink>() != null)
                    options.AddInterceptors(new AuditInterceptor(sp.GetRequiredService<AuditContext>(), session, sp));
            });
            return services;
        }

        /// <summary>Options for dotnet ef, which has no DI container.</summary>
        public static DbContextOptions<TContext> DesignTimeOptions<TContext>(string schema)
            where TContext : DbContext
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Host=localhost;Database=inventory;Username=postgres;Password=postgres";
            return new DbContextOptionsBuilder<TContext>()
                .UseNpgsql(connectionString, npgsql => ConfigureNpgsql<TContext>(npgsql, schema))
                .Options;
        }

        private static void ConfigureNpgsql<TContext>(NpgsqlDbContextOptionsBuilder npgsql, string schema)
        {
            npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema);
            npgsql.MigrationsAssembly(typeof(TContext).Assembly.FullName);
        }
    }

    /// <summary>Joins each SaveChanges to the session's ambient transaction, if one is active.</summary>
    internal sealed class TransactionEnlistmentInterceptor : SaveChangesInterceptor
    {
        private readonly DbSession _session;

        public TransactionEnlistmentInterceptor(DbSession session) => _session = session;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context != null) _session.Enlist(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null) _session.Enlist(eventData.Context);
            return ValueTask.FromResult(result);
        }
    }
}
