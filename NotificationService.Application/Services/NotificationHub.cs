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
                var groupName = $"user-{userId}";

                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogInformation($"User {userId} joined group {groupName} with connection {Context.ConnectionId}");


                // Add to role-based groups
                var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
                foreach (var role in roles)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role-{role}");
                    _logger.LogInformation($"User {userId} joined role group: role-{role}");
                }

                await _connectionManager.AddConnection(userId, Context.ConnectionId);
                _logger.LogInformation($"User {userId} connected with connection ID {Context.ConnectionId}");

                // Send acknowledgment to client (not "Connected" method)
                await Clients.Caller.SendAsync("ConnectionEstablished", new
                {
                    connectionId = Context.ConnectionId,
                    userId = userId,
                    groupName = groupName,
                    timestamp = DateTime.UtcNow
                });
                _logger.LogInformation($"✅ User {userId} fully connected to notification hub");
            }
            else
            {
                _logger.LogWarning("User connected without userId in claims");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveConnection(userId, Context.ConnectionId);
                _logger.LogInformation($"User {userId} disconnected from notification hub");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to join user group (called from client)
        public async Task JoinUserGroup()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                var groupName = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                _logger.LogInformation($"✅ User {userId} manually joined group {groupName}");

                // Send confirmation back to the caller
                await Clients.Caller.SendAsync("GroupJoined", new
                {
                    success = true,
                    groupName = groupName,
                    userId = userId
                });
            }
            else
            {
                _logger.LogWarning("JoinUserGroup called without valid userId");
                await Clients.Caller.SendAsync("GroupJoined", new
                {
                    success = false,
                    error = "No valid user ID found"
                });
            }
        }
    }
}