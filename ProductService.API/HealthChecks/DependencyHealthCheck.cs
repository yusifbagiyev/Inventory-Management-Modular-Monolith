using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProductService.API.HealthChecks
{
    public class DependencyHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DependencyHealthCheck> _logger;

        public DependencyHealthCheck(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<DependencyHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var unhealthyDependencies = new List<string>();
            var degradedDependencies = new List<string>();

            // Check if RouteService is reachable (non-critical dependency)
            try
            {
                var routeServiceUrl = _configuration["Services:RouteService"];
                if (!string.IsNullOrEmpty(routeServiceUrl))
                {
                    using var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(3);

                    var response = await client.GetAsync($"{routeServiceUrl}/health/live", cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        degradedDependencies.Add($"RouteService (Status: {response.StatusCode})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RouteService health check failed");
                degradedDependencies.Add("RouteService (Unreachable)");
            }

            // Determine overall health status
            if (unhealthyDependencies.Any())
            {
                return HealthCheckResult.Unhealthy(
                    $"Critical dependencies are unavailable: {string.Join(", ", unhealthyDependencies)}");
            }

            if (degradedDependencies.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Non-critical dependencies are unavailable: {string.Join(", ", degradedDependencies)}");
            }

            return HealthCheckResult.Healthy("All dependencies are reachable");
        }
    }
}