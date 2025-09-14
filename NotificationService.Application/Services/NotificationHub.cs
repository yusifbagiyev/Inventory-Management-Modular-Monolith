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

        // Add this method that the client is expecting
        public async Task JoinUserGroup()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogInformation($"User {userId} joined their group via explicit call");
            }
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;
            var roles = Context.User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList() ?? new List<string>();

            _logger.LogInformation($"User connecting: ID={userId}, Name={userName}, ConnectionId={Context.ConnectionId}");

            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.AddConnection(userId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

                // Add to role groups
                foreach (var role in roles)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role-{role}");
                    _logger.LogInformation($"User {userId} added to role group: role-{role}");
                }

                _logger.LogInformation($"User {userId} connected successfully");

                // Send connection confirmation
                await Clients.Caller.SendAsync("Connected", new { userId, connectionId = Context.ConnectionId });
            }
            else
            {
                _logger.LogWarning("User connected without userId");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"User disconnecting: ID={userId}, ConnectionId={Context.ConnectionId}");

            if (exception != null)
            {
                _logger.LogWarning($"User {userId} disconnected abnormally: {exception.Message}");
            }
            
            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveConnection(userId, Context.ConnectionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}