using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ApiGateway.HealthCheck
{
    public class ServicesHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ServicesHealthCheck> _logger;
        // Define all services and their health endpoints
        private readonly Dictionary<string, string> _services = new()
        {
            { "ProductService", "http://product-service/health" },
            { "IdentityService", "http://identity-service/health" },
            { "RouteService", "http://route-service/health" },
            { "ApprovalService", "http://approval-service/health" },
            { "NotificationService", "http://notification-service/health" }
        };

        public ServicesHealthCheck(
            IHttpClientFactory httpClientFactory,
            ILogger<ServicesHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var healthChecks = new ConcurrentDictionary<string, string>();
            var tasks = new List<Task>();

            foreach (var service in _services)
            {
                tasks.Add(CheckServiceHealth(service.Key, service.Value, healthChecks, cancellationToken));
            }

            await Task.WhenAll(tasks);

            // Analyze results
            var unhealthyServices = healthChecks.Where(x => x.Value == "Unhealthy").Select(x => x.Key).ToList();
            var degradedServices = healthChecks.Where(x => x.Value == "Degraded").Select(x => x.Key).ToList();
            var healthyServices = healthChecks.Where(x => x.Value == "Healthy").Select(x => x.Key).ToList();

            var data = new Dictionary<string, object>
            {
                { "healthy", healthyServices },
                { "degraded", degradedServices },
                { "unhealthy", unhealthyServices },
                { "totalServices", _services.Count },
                { "healthyCount", healthyServices.Count },
                { "timestamp", DateTime.UtcNow }
            };

            if (unhealthyServices.Any())
            {
                // If critical services are down, the whole system is unhealthy
                var criticalServices = new[] { "IdentityService", "ProductService" };
                if (unhealthyServices.Any(s => criticalServices.Contains(s)))
                {
                    return HealthCheckResult.Unhealthy(
                        $"Critical services are down: {string.Join(", ", unhealthyServices)}",
                        null,
                        data);
                }

                return HealthCheckResult.Degraded(
                    $"Non-critical services are down: {string.Join(", ", unhealthyServices)}",
                    null,
                    data);
            }

            if (degradedServices.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Some services are degraded: {string.Join(", ", degradedServices)}",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy("All services are healthy", data);
        }

        private async Task CheckServiceHealth(
            string serviceName,
            string healthUrl,
            ConcurrentDictionary<string, string> results,
            CancellationToken cancellationToken)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.GetAsync(healthUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content);
                    results[serviceName] = healthResponse?.Status ?? "Unknown";
                }
                else
                {
                    results[serviceName] = "Unhealthy";
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Health check timeout for {ServiceName}", serviceName);
                results[serviceName] = "Timeout";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for {ServiceName}", serviceName);
                results[serviceName] = "Unhealthy";
            }
        }

        private record HealthResponse
        {
            public string Status { get; set; } = "Unknown";
        }
    }
}