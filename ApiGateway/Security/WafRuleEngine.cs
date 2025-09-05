using System.Text.RegularExpressions;

namespace ApiGateway.Security
{
    public interface IWafRuleEngine
    {
        Task<WafValidationResult> ValidateRequestAsync(HttpContext context);
    }

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

    public class WafValidationResult
    {
        public bool IsValid { get; set; }
        public string? Reason { get; set; }
        public List<string>? BlockedBy { get; set; }
    }

    public interface IWafRule
    {
        Task<WafValidationResult> ValidateAsync(HttpContext context);
    }

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

    public class RequestSizeRule : IWafRule
    {
        private const long MaxRequestSize = 10 * 1024 * 1024; // 10MB

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            if (context.Request.ContentLength > MaxRequestSize)
            {
                return Task.FromResult(new WafValidationResult
                {
                    IsValid = false,
                    Reason = $"Request size exceeds maximum allowed size of {MaxRequestSize} bytes",
                    BlockedBy = new List<string> { "RequestSizeRule" }
                });
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }

    public class FileUploadRule : IWafRule
    {
        private static readonly string[] BlockedExtensions =
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".pif", ".scr",
            ".vbs", ".js", ".jar", ".zip", ".rar", ".sh", ".ps1"
        };

        public async Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            if (context.Request.HasFormContentType)
            {
                try
                {
                    var form = await context.Request.ReadFormAsync();
                    foreach (var file in form.Files)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (BlockedExtensions.Contains(extension))
                        {
                            return new WafValidationResult
                            {
                                IsValid = false,
                                Reason = $"Blocked file extension: {extension}",
                                BlockedBy = new List<string> { "FileUploadRule" }
                            };
                        }
                    }
                }
                catch
                {
                    // If form reading fails, allow request to proceed
                }
            }

            return new WafValidationResult { IsValid = true };
        }
    }

    public class RateLimitRule : IWafRule
    {
        private static readonly Dictionary<string, List<DateTime>> RequestHistory = new();
        private static readonly object LockObject = new();
        private const int MaxRequestsPerMinute = 60;

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            var clientId = GetClientIdentifier(context);
            var now = DateTime.UtcNow;

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