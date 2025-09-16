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
                // Store connection for this user
                await _connectionManager.AddConnection(userId, Context.ConnectionId);

                // Add to user-specific group - this is crucial for targeted notifications
                var userGroup = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);

                // Add to role groups for role-based notifications
                var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
                foreach (var role in roles)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role-{role}");
                    _logger.LogInformation($"User {userId} added to role group: role-{role}");
                }

                _logger.LogInformation($"User {userId} connected with ID {Context.ConnectionId}");

                // Send connection confirmation with initial data
                await Clients.Caller.SendAsync("ConnectionEstablished", new
                {
                    connectionId = Context.ConnectionId,
                    userId = userId,
                    timestamp = DateTime.UtcNow,
                    groups = roles.Select(r => $"role-{r}").Append(userGroup)
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
                _logger.LogInformation($"User {userId} disconnected: {exception?.Message ?? "Normal disconnect"}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to manually trigger a test notification
        public async Task TestNotification()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("ReceiveNotification", new
                {
                    id = 0,
                    type = "System",
                    title = "Test Notification",
                    message = "SignalR connection is working properly!",
                    createdAt = DateTime.UtcNow,
                    isRead = false
                });
            }
        }
    }
}