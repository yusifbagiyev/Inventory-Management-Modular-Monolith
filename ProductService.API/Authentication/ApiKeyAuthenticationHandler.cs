using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ProductService.API.Authentication
{
    /// <summary>Checks the X-Api-Key of ServiceDesk and other integrations against ApiKeys, set only by env vars.</summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var values) || string.IsNullOrEmpty(values.FirstOrDefault()))
                return Task.FromResult(AuthenticateResult.NoResult());

            var provided = Encoding.UTF8.GetBytes(values.First()!);
            var client = (_configuration.GetSection("ApiKeys").Get<List<ApiKeyConfig>>() ?? [])
                .FirstOrDefault(c => !string.IsNullOrEmpty(c.Key)
                    && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(c.Key), provided));

            if (client == null)
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, client.ServiceName),
                new(ClaimTypes.NameIdentifier, client.ServiceId),
            };
            claims.AddRange(client.Permissions.Select(p => new Claim("permission", p)));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

    public record ApiKeyConfig
    {
        public string Key { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }
}
