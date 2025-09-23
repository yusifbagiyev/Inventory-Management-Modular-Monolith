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
                    groups = roles.Select(r => $"role-{r}").Append(userGroup),
                    silent = true // This flag tells the client not to show a popup
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

        public async Task JoinUserGroup()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var userGroup = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
                _logger.LogInformation($"User {userId} explicitly joined group {userGroup}");
            }
        }


        // Method to manually trigger a test notification
        public async Task SendTestNotification()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("ReceiveNotification", new
                {
                    id = 0,
                    type = "System",
                    title = "Manual Test Notification",
                    message = "This is a manual test notification!",
                    createdAt = DateTime.UtcNow,
                    isRead = false,
                    isTest = true
                });
            }
        }


        // Add a method for silent connection testing that doesn't trigger popups
        public async Task TestConnection()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Send a silent test response - no popup will be shown
                await Clients.Caller.SendAsync("ConnectionTest", new
                {
                    status = "connected",
                    timestamp = DateTime.UtcNow,
                    silent = true
                });
            }
        }
    }
}