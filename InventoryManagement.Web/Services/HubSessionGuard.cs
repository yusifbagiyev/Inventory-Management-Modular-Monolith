using System.Security.Claims;
using InventoryManagement.Web.Extensions;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using IdentityAuth = IdentityService.Application.Services.IAuthService;

namespace InventoryManagement.Web.Services
{
    /// <summary>Closes a hub connection once its user's session has ended, since the cookie is only read when the connection opens.</summary>
    public sealed class HubSessionGuard : IHubFilter
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<HubSessionGuard> _logger;

        public HubSessionGuard(IServiceScopeFactory scopes, ILogger<HubSessionGuard> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            await next(context);

            var connection = context.Context;
            if (connection.User?.Identity?.IsAuthenticated != true) return;

            var watch = new Watch(connection, _scopes, _logger);
            connection.Features.Get<IConnectionHeartbeatFeature>()?.OnHeartbeat(static state => ((Watch)state).Tick(), watch);
        }

        /// <summary>The same rules as the cookie's own re-check: the user is active, the stamp is unchanged and the sign-in is not too old.</summary>
        private static async Task<bool> SessionEndedAsync(ClaimsPrincipal user, IdentityAuth auth)
        {
            if (!int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return true;
            if (DateTimeOffset.UtcNow - UserPrincipalFactory.SignedInAt(user) > UserPrincipalFactory.MaxSessionAge)
                return true;

            // Null for a deactivated or deleted user
            var stamp = await auth.GetSessionStampAsync(userId);
            var held = user.FindFirst(AuthenticationExtensions.SessionStampClaim)?.Value;
            // A session from before the stamp claim existed holds none to compare
            return stamp == null || (held != null && held != stamp);
        }

        /// <summary>One connection's check, started from its heartbeat when it is due.</summary>
        private sealed class Watch
        {
            private readonly HubCallerContext _connection;
            private readonly IServiceScopeFactory _scopes;
            private readonly ILogger _logger;
            private long _nextCheck = Environment.TickCount64 + (long)CheckInterval.TotalMilliseconds;
            private int _busy;

            public Watch(HubCallerContext connection, IServiceScopeFactory scopes, ILogger logger)
            {
                _connection = connection;
                _scopes = scopes;
                _logger = logger;
            }

            /// <summary>Runs every second on the server's heartbeat timer, so it only hands a due check to the thread pool.</summary>
            public void Tick()
            {
                if (Environment.TickCount64 < Volatile.Read(ref _nextCheck) || Interlocked.Exchange(ref _busy, 1) == 1)
                    return;
                _ = Task.Run(CheckAsync);
            }

            private async Task CheckAsync()
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    if (await SessionEndedAsync(_connection.User!, scope.ServiceProvider.GetRequiredService<IdentityAuth>()))
                    {
                        _logger.LogInformation("Closing hub connection {ConnectionId} because the session of user {UserId} has ended",
                            _connection.ConnectionId, _connection.UserIdentifier);
                        // Stays busy, so a closing connection is not checked again
                        _connection.Abort();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // A failed check keeps the connection, or a database hiccup would disconnect everyone
                    _logger.LogWarning(ex, "Could not re-check the session of hub connection {ConnectionId}", _connection.ConnectionId);
                }

                Volatile.Write(ref _nextCheck, Environment.TickCount64 + (long)CheckInterval.TotalMilliseconds);
                Volatile.Write(ref _busy, 0);
            }
        }
    }
}
