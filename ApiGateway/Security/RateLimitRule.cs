using ApiGateway.Dto;
using ApiGateway.Interfaces;

namespace ApiGateway.Security
{
    public class RateLimitRule : IWafRule
    {
        private static readonly Dictionary<string, List<DateTime>> RequestHistory = new();
        private static readonly object LockObject = new();
        private const int MaxRequestsPerMinute = 60;

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var clientId = GetClientIdentifier(context);
            var now = DateTime.Now;

            lock (LockObject)
            {
                if (!RequestHistory.ContainsKey(clientId))
                    RequestHistory[clientId] = new List<DateTime>();

                // Clean old entries
                RequestHistory[clientId] = RequestHistory[clientId]
                    .Where(dt => dt > now.AddMinutes(-1))
                    .ToList();

                RequestHistory[clientId].Add(now);

                if (RequestHistory[clientId].Count > MaxRequestsPerMinute)
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = "Rate limit exceeded",
                        BlockedBy = new List<string> { "RateLimitRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }

        private string GetClientIdentifier(HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
                return context.User.Identity.Name ?? "anonymous";

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}