using ApiGateway.Dto;
using ApiGateway.Interfaces;

namespace ApiGateway.Security
{
    public class BotDetectionRule : IWafRule
    {
        private static readonly string[] BotUserAgents =
        {
            "bot", "crawler", "spider", "scraper", "wget", "curl"
        };

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var userAgent = context.Request.Headers["User-Agent"].ToString().ToLowerInvariant();

            if (string.IsNullOrEmpty(userAgent))
            {
                return Task.FromResult(new WafValidationResult
                {
                    IsValid = false,
                    Reason = "Missing User-Agent header",
                    BlockedBy = new List<string> { "BotDetectionRule" }
                });
            }

            foreach (var bot in BotUserAgents)
            {
                if (userAgent.Contains(bot))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = $"Bot detected: {bot}",
                        BlockedBy = new List<string> { "BotDetectionRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }
}