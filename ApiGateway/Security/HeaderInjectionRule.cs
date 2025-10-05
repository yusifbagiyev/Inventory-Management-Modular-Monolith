using ApiGateway.Dto;
using ApiGateway.Interfaces;

namespace ApiGateway.Security
{
    public class HeaderInjectionRule : IWafRule
    {
        private static readonly string[] DangerousHeaders =
        {
            "X-Forwarded-Host",
            "X-Original-URL",
            "X-Rewrite-URL"
        };

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            foreach (var header in context.Request.Headers)
            {
                // Check for CRLF injection
                if (header.Value.Any(v => v.Contains('\r') || v.Contains('\n')))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = "Header injection attempt detected",
                        BlockedBy = new List<string> { "HeaderInjectionRule" }
                    });
                }

                // Check for dangerous headers
                if (DangerousHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = $"Dangerous header detected: {header.Key}",
                        BlockedBy = new List<string> { "HeaderInjectionRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }
}