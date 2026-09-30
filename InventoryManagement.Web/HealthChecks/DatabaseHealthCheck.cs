using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace InventoryManagement.Web.HealthChecks
{
    /// <summary>Healthy when the database answers; the app is useless without it.</summary>
    public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                // The exception goes to the log; the response body only says "Unhealthy".
                return HealthCheckResult.Unhealthy("Database unreachable", ex);
            }
        }
    }
}
