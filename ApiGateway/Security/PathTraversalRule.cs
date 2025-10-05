using ApiGateway.Dto;
using ApiGateway.Interfaces;
using System.Text.RegularExpressions;

namespace ApiGateway.Security
{
    public class PathTraversalRule : IWafRule
    {
        private static readonly Regex[] PathPatterns =
        {
            new Regex(@"\.\./"),
            new Regex(@"\.\.\\"),
            new Regex(@"%2e%2e[\/\\]", RegexOptions.IgnoreCase),
            new Regex(@"%252e%252e[\/\\]", RegexOptions.IgnoreCase),
            new Regex(@"\.\./\.\./"),
            new Regex(@"\.\.;/"),
            new Regex(@"/etc/passwd"),
            new Regex(@"c:\\", RegexOptions.IgnoreCase),
            new Regex(@"/proc/self")
        };

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var path = context.Request.Path.ToString();
            var queryString = context.Request.QueryString.ToString();

            var toCheck = $"{path} {queryString}";

            foreach (var pattern in PathPatterns)
            {
                if (pattern.IsMatch(toCheck))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = "Path traversal attempt detected",
                        BlockedBy = new List<string> { "PathTraversalRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }

}