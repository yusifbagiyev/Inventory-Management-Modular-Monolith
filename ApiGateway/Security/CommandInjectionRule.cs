using ApiGateway.Dto;
using ApiGateway.Interfaces;
using System.Text.RegularExpressions;

namespace ApiGateway.Security
{
    public class CommandInjectionRule : IWafRule
    {
        private static readonly Regex[] CommandPatterns =
        {
            new Regex(@"(\||;|`|>|<|\$\(|\${)", RegexOptions.None),
            new Regex(@"(cmd\.exe|powershell|bash|sh|nc|netcat|telnet|eval)", RegexOptions.IgnoreCase),
            new Regex(@"(/bin/|/usr/bin/|/usr/local/bin/)", RegexOptions.IgnoreCase)
        };

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var queryString = context.Request.QueryString.ToString();

            foreach (var pattern in CommandPatterns)
            {
                if (pattern.IsMatch(queryString))
                {
                    return Task.FromResult(new WafValidationResult
                    {
                        IsValid = false,
                        Reason = "Command injection attempt detected",
                        BlockedBy = new List<string> { "CommandInjectionRule" }
                    });
                }
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }

}
}