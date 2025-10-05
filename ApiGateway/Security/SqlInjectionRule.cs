using ApiGateway.Dto;
using ApiGateway.Interfaces;
using System.Text.RegularExpressions;

namespace ApiGateway.Security
{
    public class SqlInjectionRule : IWafRule
    {
        private static readonly Regex[] SqlPatterns =
        {
            new Regex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|UNION|ALTER|CREATE|EXEC|EXECUTE|DECLARE|CAST|CONVERT)\b)", RegexOptions.IgnoreCase),
            new Regex(@"(--|;|\/\*|\*\/|@@|@|char|nchar|varchar|nvarchar|alter|begin|cast|create|cursor|declare|delete|drop|end|exec|execute|fetch|insert|kill|select|sys|sysobjects|syscolumns|table|update)", RegexOptions.IgnoreCase),
            new Regex(@"(\bOR\b\s*\d+\s*=\s*\d+|\bAND\b\s*\d+\s*=\s*\d+)", RegexOptions.IgnoreCase),
            new Regex(@"(xp_|sp_|0x|OPENROWSET|OPENDATASOURCE)", RegexOptions.IgnoreCase)
        };

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var queryString = context.Request.QueryString.ToString();
            var path = context.Request.Path.ToString();

            var toCheck = $"{queryString} {path}";

            foreach (var pattern in SqlPatterns)
            {
                if (pattern.IsMatch(toCheck))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = "Potential SQL injection detected",
                        BlockedBy = new List<string> { "SqlInjectionRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }

}
