using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Services
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(IConnectionManager connectionManager, ILogger<NotificationHub> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // Add to user-specific group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

                // Add to role-based groups
                var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
                foreach (var role in roles)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role-{role}");
                }

                await _connectionManager.AddConnection(userId, Context.ConnectionId);
                _logger.LogInformation($"User {userId} connected with connection ID {Context.ConnectionId}");

                // Send acknowledgment to client (not "Connected" method)
                await Clients.Caller.SendAsync("ConnectionEstablished", new
                {
                    connectionId = Context.ConnectionId,
                    userId = userId,
                    timestamp = DateTime.UtcNow
                });
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveConnection(userId, Context.ConnectionId);
                _logger.LogInformation($"User {userId} disconnected");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to join user group (called from client)
        public async Task JoinUserGroup()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogInformation($"User {userId} joined their notification group");
            }
        }

        // Method to send notification to specific user
        public async Task SendToUser(string userId, object notification)
        {
            await Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", notification);
        }

        // Method to send notification to role
        public async Task SendToRole(string role, object notification)
        {
            await Clients.Group($"role-{role}").SendAsync("ReceiveNotification", notification);
        }
    }
}