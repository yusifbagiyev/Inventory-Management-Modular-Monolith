using ApiGateway.Dto;
using ApiGateway.Interfaces;

namespace ApiGateway.Security
{
    public class WafRuleEngine : IWafRuleEngine
    {
        private readonly List<IWafRule> _rules;
        private readonly ILogger<WafRuleEngine> _logger;

        public WafRuleEngine(ILogger<WafRuleEngine> logger)
        {
            _logger = logger;
            _rules = new List<IWafRule>
            {
                new SqlInjectionRule(),
                new XssRule(),
                new PathTraversalRule(),
                new CommandInjectionRule(),
                new HeaderInjectionRule(),
                new RequestSizeRule(),
                new FileUploadRule(),
                new RateLimitRule(),
                new BotDetectionRule()
            };
        }

        public async Task<WafValidationResult> ValidateRequestAsync(HttpContext context)
        {
            var results = new List<WafValidationResult>();

            foreach (var rule in _rules)
            {
                var result = await rule.ValidateAsync(context);
                if (!result.IsValid)
                {
                    _logger.LogWarning("WAF Rule '{RuleName}' blocked request: {Reason}",
                        rule.GetType().Name, result.Reason);
                    results.Add(result);
                }
            }

            if (results.Any())
            {
                return new WafValidationResult
                {
                    IsValid = false,
                    Reason = string.Join("; ", results.Select(r => r.Reason)),
                    BlockedBy = results.Select(r => r.BlockedBy).ToList().SelectMany(b => b ?? new List<string>()).Distinct().ToList()
                };
            }

            return new WafValidationResult { IsValid = true };
        }
    }
}