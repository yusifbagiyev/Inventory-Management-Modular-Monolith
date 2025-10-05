using System.Collections.Concurrent;

namespace ApiGateway.Security
{
    public interface IRequestThrottler
    {
        Task<ThrottleResult> ShouldThrottleAsync(HttpContext context);
    }

    public class RequestThrottler : IRequestThrottler
    {
        private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();
        private readonly ILogger<RequestThrottler> _logger;

        public RequestThrottler(ILogger<RequestThrottler> logger)
        {
            _logger = logger;
        }

        public Task<ThrottleResult> ShouldThrottleAsync(HttpContext context)
        {
            var endpoint = context.Request.Path.ToString().ToLowerInvariant();
            var clientId = GetClientIdentifier(context);
            var key = $"{clientId}:{endpoint}";

            var window = _windows.GetOrAdd(key, _ => new SlidingWindow());

            var limits = GetLimitsForEndpoint(endpoint);
            var result = window.AllowRequest(limits);

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Request throttled for {ClientId} on endpoint {Endpoint}",
                    clientId, endpoint);
            }

            return Task.FromResult(result);
        }

        private ThrottleLimits GetLimitsForEndpoint(string endpoint)
        {
            // Define different limits for different endpoints
            if (endpoint.StartsWith("/api/auth/login"))
            {
                return new ThrottleLimits { RequestsPerMinute = 5, BurstSize = 2 };
            }
            else if (endpoint.StartsWith("/api/auth/register"))
            {
                return new ThrottleLimits { RequestsPerMinute = 3, BurstSize = 1 };
            }
            else if (endpoint.StartsWith("/api/products") && endpoint.Contains("upload"))
            {
                return new ThrottleLimits { RequestsPerMinute = 10, BurstSize = 3 };
            }
            else if (endpoint.StartsWith("/api/"))
            {
                return new ThrottleLimits { RequestsPerMinute = 60, BurstSize = 10 };
            }
            else
            {
                return new ThrottleLimits { RequestsPerMinute = 120, BurstSize = 20 };
            }
        }

        private string GetClientIdentifier(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
                return $"user:{context.User.Identity.Name}";

            return $"ip:{context.Connection.RemoteIpAddress}";
        }

        private class SlidingWindow
        {
            private readonly Queue<DateTime> _requests = new();
            private readonly object _lock = new();

            public ThrottleResult AllowRequest(ThrottleLimits limits)
            {
                lock (_lock)
                {
                    var now = DateTime.Now;
                    var windowStart = now.AddMinutes(-1);

                    // Remove old requests
                    while (_requests.Count > 0 && _requests.Peek() < windowStart)
                    {
                        _requests.Dequeue();
                    }

                    // Check burst limit
                    var recentRequests = _requests.Where(r => r > now.AddSeconds(-1)).Count();
                    if (recentRequests >= limits.BurstSize)
                    {
                        return new ThrottleResult
                        {
                            IsAllowed = false,
                            RetryAfter = TimeSpan.FromSeconds(1),
                            Reason = "Burst limit exceeded"
                        };
                    }

                    // Check rate limit
                    if (_requests.Count >= limits.RequestsPerMinute)
                    {
                        var oldestInWindow = _requests.Peek();
                        var retryAfter = oldestInWindow.AddMinutes(1) - now;

                        return new ThrottleResult
                        {
                            IsAllowed = false,
                            RetryAfter = retryAfter,
                            Reason = "Rate limit exceeded"
                        };
                    }

                    _requests.Enqueue(now);
                    return new ThrottleResult { IsAllowed = true };
                }
            }
        }

        private record ThrottleLimits
        {
            public int RequestsPerMinute { get; set; }
            public int BurstSize { get; set; }
        }
    }

    public record ThrottleResult
    {
        public bool IsAllowed { get; set; }
        public TimeSpan? RetryAfter { get; set; }
        public string? Reason { get; set; }
    }
}