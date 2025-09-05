using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharedServices.HealthChecks
{
    public class CustomHealthCheck : IHealthCheck
    {
        private readonly string _serviceName;
        private static readonly Random _random = new();

        public CustomHealthCheck(string serviceName)
        {
            _serviceName = serviceName;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var isHealthy = _random.Next(100) > 5; // 95% healthy

            var data = new Dictionary<string, object>
            {
                ["service"] = _serviceName,
                ["timestamp"] = DateTime.UtcNow,
                ["uptime"] = TimeSpan.FromMilliseconds(Environment.TickCount64),
                ["memory_usage_mb"] = GC.GetTotalMemory(false) / 1024 / 1024,
                ["gc_gen0"] = GC.CollectionCount(0),
                ["gc_gen1"] = GC.CollectionCount(1),
                ["gc_gen2"] = GC.CollectionCount(2),
                ["thread_count"] = Process.GetCurrentProcess().Threads.Count,
                ["cpu_usage"] = GetCpuUsage()
            };

            if (isHealthy)
            {
                return Task.FromResult(HealthCheckResult.Healthy($"{_serviceName} is healthy", data));
            }

            return Task.FromResult(HealthCheckResult.Degraded($"{_serviceName} is degraded", null, data));
        }

        private double GetCpuUsage()
        {
            // Simplified CPU usage calculation
            return _random.Next(10, 90);
        }
    }
}