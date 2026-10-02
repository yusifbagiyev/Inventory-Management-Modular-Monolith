using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SharedServices.Auditing
{
    /// <summary>Who is acting in this request and as part of which action.</summary>
    public sealed class AuditContext
    {
        private readonly IHttpContextAccessor? _httpContext;
        private readonly string _fallbackCorrelation = Guid.NewGuid().ToString("N");

        public AuditContext(IHttpContextAccessor? httpContext = null)
        {
            _httpContext = httpContext;
        }

        /// <summary>Set by AuditActionBehavior from the outermost MediatR request.</summary>
        public string? Action { get; set; }

        private HttpContext? Http => _httpContext?.HttpContext;

        public string CorrelationId => Http?.TraceIdentifier ?? _fallbackCorrelation;

        public int? UserId
            => int.TryParse(Http?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        public string? UserName
        {
            get
            {
                var user = Http?.User;
                if (user?.Identity?.IsAuthenticated != true) return null;
                var fullName = $"{user.FindFirst("FirstName")?.Value} {user.FindFirst("LastName")?.Value}".Trim();
                return fullName.Length > 0 ? $"{fullName} ({user.Identity.Name})" : user.Identity.Name;
            }
        }

        /// <summary>Client address from the forwarded-headers middleware, never the raw header.</summary>
        public string? IpAddress => Http?.Connection.RemoteIpAddress?.ToString();

        /// <summary>The command name, else Controller.Action, else the method and path.</summary>
        public string CurrentAction
        {
            get
            {
                if (!string.IsNullOrEmpty(Action)) return Action;
                var route = Http?.Request.RouteValues;
                if (route?["controller"] is string controller)
                    return route["action"] is string action ? $"{controller}.{action}" : controller;
                return Http != null ? $"{Http.Request.Method} {Http.Request.Path}" : "System";
            }
        }

        /// <summary>Builds a record, where userId stands in for the signed-in user during a sign-in before the cookie exists.</summary>
        public AuditRecord Record(string entityType, string? entityId, string? label, string operation,
            IReadOnlyList<AuditFieldChange>? changes = null, int? userId = null, string? userName = null)
            => new(DateTime.Now, CorrelationId, userId ?? UserId, userName ?? UserName, IpAddress, CurrentAction,
                   entityType, entityId, label, operation, changes ?? []);
    }
}
