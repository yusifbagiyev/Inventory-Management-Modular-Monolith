using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharedServices.Logging
{
    public static class LogSanitizer
    {
        private static readonly HashSet<string> SensitiveFieldNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "pwd", "pass", "secret", "token", "key", "apikey", "api_key",
            "authorization", "auth", "credential", "connectionstring", "conn_string",
            "ssn", "socialsecuritynumber", "creditcard", "cc", "cvv", "pin",
            "refreshtoken", "refresh_token", "access_token", "accesstoken",
            "private_key", "privatekey", "client_secret", "clientsecret"
        };

        private static readonly Dictionary<string, Regex> PatternMatchers = new()
        {
            ["Email"] = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled),
            ["Phone"] = new Regex(@"\+?[1-9]\d{1,14}", RegexOptions.Compiled),
            ["CreditCard"] = new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled),
            ["SSN"] = new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled),
            ["JWT"] = new Regex(@"eyJ[A-Za-z0-9-_]+\.eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+", RegexOptions.Compiled),
            ["ApiKey"] = new Regex(@"[a-zA-Z0-9]{32,}", RegexOptions.Compiled),
            ["BearerToken"] = new Regex(@"Bearer\s+[A-Za-z0-9-_]+", RegexOptions.Compiled),
            ["IPv4"] = new Regex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b", RegexOptions.Compiled)
        };

        public static string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // Sanitize JSON content
            if (IsJson(message))
            {
                return SanitizeJson(message);
            }

            // Apply pattern-based sanitization
            foreach (var pattern in PatternMatchers)
            {
                message = pattern.Value.Replace(message, match =>
                {
                    return pattern.Key switch
                    {
                        "Email" => MaskEmail(match.Value),
                        "Phone" => MaskPhone(match.Value),
                        "CreditCard" => "****-****-****-****",
                        "SSN" => "***-**-****",
                        "JWT" => "eyJ***.[REDACTED]",
                        "ApiKey" => "[API_KEY_REDACTED]",
                        "BearerToken" => "Bearer [TOKEN_REDACTED]",
                        "IPv4" => MaskIpAddress(match.Value),
                        _ => "[REDACTED]"
                    };
                });
            }

            return message;
        }

        public static object? SanitizeObject(object obj)
        {
            if (obj == null) return obj;

            try
            {
                var json = JsonConvert.SerializeObject(obj);
                var sanitized = SanitizeJson(json);
                return JsonConvert.DeserializeObject(sanitized, obj.GetType()) ?? obj;
            }
            catch
            {
                return obj; // Return original if sanitization fails
            }
        }

        private static string SanitizeJson(string json)
        {
            try
            {
                var jObject = JToken.Parse(json);
                SanitizeJsonToken(jObject);
                return jObject.ToString();
            }
            catch
            {
                return json; // Return original if parsing fails
            }
        }

        private static void SanitizeJsonToken(JToken token)
        {
            if (token.Type == JTokenType.Object)
            {
                foreach (var property in token.Children<JProperty>().ToList())
                {
                    if (ShouldRedactField(property.Name))
                    {
                        property.Value = JToken.FromObject("[REDACTED]");
                    }
                    else
                    {
                        SanitizeJsonToken(property.Value);
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in token.Children())
                {
                    SanitizeJsonToken(item);
                }
            }
            else if (token.Type == JTokenType.String)
            {
                var value = token.ToString();
                foreach (var pattern in PatternMatchers)
                {
                    if (pattern.Value.IsMatch(value))
                    {
                        token.Replace(JToken.FromObject("[REDACTED]"));
                        break;
                    }
                }
            }
        }

        private static bool ShouldRedactField(string fieldName)
        {
            return SensitiveFieldNames.Any(sensitive =>
                fieldName.IndexOf(sensitive, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return "[EMAIL_REDACTED]";

            var localPart = parts[0];
            var domain = parts[1];

            if (localPart.Length <= 3)
                return $"***@{domain}";

            return $"{localPart.Substring(0, 3)}***@{domain}";
        }

        private static string MaskPhone(string phone)
        {
            if (phone.Length <= 4) return "****";
            return phone.Substring(0, phone.Length - 4) + "****";
        }

        private static string MaskIpAddress(string ip)
        {
            var parts = ip.Split('.');
            if (parts.Length != 4) return "[IP_REDACTED]";
            return $"{parts[0]}.{parts[1]}.***.***";
        }

        private static bool IsJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim();
            return (input.StartsWith("{") && input.EndsWith("}")) ||
                   (input.StartsWith("[") && input.EndsWith("]"));
        }
    }
}