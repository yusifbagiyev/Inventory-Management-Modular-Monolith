using IdentityService.Application.DTOs;
using SharedServices.Auditing;

namespace IdentityService.API
{
    /// <summary>Writes the sign-in, failed sign-in and sign-out records, the same for the pages and the API.</summary>
    public sealed class SessionAudit
    {
        /// <summary>Size of the audit log's user name column, which is shorter than the username a sign-in form accepts.</summary>
        private const int MaxUserNameLength = 200;

        private readonly AuditContext _audit;
        private readonly IAuditSink _auditLog;
        private readonly ILogger<SessionAudit> _logger;

        public SessionAudit(AuditContext audit, IAuditSink auditLog, ILogger<SessionAudit> logger)
        {
            _audit = audit;
            _auditLog = auditLog;
            _logger = logger;
        }

        public Task SignedInAsync(UserDto user)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return WriteAsync(AuditOperations.SignedIn, user.Id,
                fullName.Length > 0 ? $"{fullName} ({user.Username})" : user.Username, user.Username);
        }

        /// <summary>Never throws, so a failed sign-in always gets its ordinary answer.</summary>
        public async Task SignInFailedAsync(string username, string reason)
        {
            try
            {
                await WriteAsync(AuditOperations.SignInFailed, null, username, username, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not record the failed sign-in for {Username}", username);
            }
        }

        /// <summary>Records the sign-out of the user this request is signed in as.</summary>
        public Task SignedOutAsync(string? username)
            => WriteAsync(AuditOperations.SignedOut, _audit.UserId, _audit.UserName, username);

        private Task WriteAsync(string operation, int? userId, string? userName, string? username, string? reason = null)
            => _auditLog.WriteAsync([_audit.Record("Session", userId?.ToString(), username, operation,
                reason == null ? null : [new AuditFieldChange("Reason", null, reason)], userId, Cap(userName))]);

        private static string? Cap(string? value)
            => value is { Length: > MaxUserNameLength } ? value[..MaxUserNameLength] : value;
    }
}
