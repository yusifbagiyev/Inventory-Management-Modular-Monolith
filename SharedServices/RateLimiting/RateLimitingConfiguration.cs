using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SharedServices.RateLimiting
{
    public static class RateLimitingConfiguration
    {
        public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Global rate limiter for all requests
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    httpContext => RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Specific policy for authenticated users
                options.AddPolicy("authenticated", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.Identity?.Name ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 200,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 50
                        }));

                // Strict policy for login/register endpoints
                options.AddPolicy("auth-endpoints", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetClientIpAddress(context) ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(15),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 2
                        }));

                // API endpoints rate limiting
                options.AddPolicy("api", context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: GetPartitionKey(context),
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 20
                        }));

                // Critical operations rate limiting
                options.AddPolicy("critical-operations", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.Identity?.Name ?? GetClientIpAddress(context) ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(5),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 5
                        }));

                // File upload rate limiting
                options.AddPolicy("file-upload", context =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: context.User?.Identity?.Name ?? "anonymous",
                        factory: _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = 10,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 5,
                            ReplenishmentPeriod = TimeSpan.FromMinutes(10),
                            TokensPerPeriod = 5,
                            AutoReplenishment = true
                        }));

                // Custom rejection response
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";

                    var response = new
                    {
                        error = "Too many requests",
                        message = "Rate limit exceeded. Please try again later.",
                        retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                            ? retryAfter.TotalSeconds
                            : 60
                    };

                    await context.HttpContext.Response.WriteAsJsonAsync(response, token);
                };
            });

            return services;
        }

        private static string GetPartitionKey(HttpContext context)
        {
            // Prioritize authenticated user, then IP address
            if (context.User?.Identity?.IsAuthenticated == true)
                return context.User.Identity.Name ?? "authenticated";

            return GetClientIpAddress(context) ?? "anonymous";
        }

        private static string? GetClientIpAddress(HttpContext context)
        {
            // Check for proxy headers first
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
                if (IsValidIpAddress(ip))
                    return ip;
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && IsValidIpAddress(realIp))
                return realIp;

            return context.Connection.RemoteIpAddress?.ToString();
        }

        private static bool IsValidIpAddress(string? ip)
        {
            return !string.IsNullOrEmpty(ip) &&
                   IPAddress.TryParse(ip, out _) &&
                   ip != "::1" &&
                   ip != "127.0.0.1";
        }
    }
}