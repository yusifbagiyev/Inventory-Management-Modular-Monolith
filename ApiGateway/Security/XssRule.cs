using ApiGateway.Dto;
using ApiGateway.Interfaces;
using System.Text.RegularExpressions;

namespace ApiGateway.Security
{
    public class XssRule : IWafRule
    {
        private static readonly Regex[] XssPatterns =
        {
            new Regex(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase),
            new Regex(@"javascript\s*:", RegexOptions.IgnoreCase),
            new Regex(@"on\w+\s*=", RegexOptions.IgnoreCase),
            new Regex(@"<iframe[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<object[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<embed[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<applet[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<meta[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<img[^>]*\s+src[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<body[^>]*\s+onload[^>]*>", RegexOptions.IgnoreCase)
        };

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var queryString = context.Request.QueryString.ToString();
            var path = context.Request.Path.ToString();

            var toCheck = $"{queryString} {path}";

            foreach (var pattern in XssPatterns)
            {
                if (pattern.IsMatch(toCheck))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = "Potential XSS attack detected",
                        BlockedBy = new List<string> { "XssRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }

}