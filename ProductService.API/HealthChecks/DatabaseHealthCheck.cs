using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductService.Infrastructure.Data;

namespace ProductService.API.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(
            IServiceProvider serviceProvider,
            ILogger<DatabaseHealthCheck> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

                // First, check basic connectivity
                var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    return HealthCheckResult.Unhealthy("Cannot connect to database");
                }

                // Then, verify we can actually query data
                // This catches issues like migration problems or permission issues
                var productCount = await dbContext.Products
                    .Take(1)
                    .CountAsync(cancellationToken);

                // Check if migrations are pending (important for production)
                var pendingMigrations = await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken);

                if (pendingMigrations.Any())
                {
                    return HealthCheckResult.Degraded(
                        $"Database is accessible but has {pendingMigrations.Count()} pending migrations");
                }

                return HealthCheckResult.Healthy($"Database is fully operational");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy($"Database check failed: {ex.Message}");
            }
        }
    }
}