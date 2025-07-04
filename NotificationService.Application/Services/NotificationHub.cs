using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Application.Services
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly IConnectionManager _connectionManager;
        public NotificationHub(IConnectionManager connectionManager)
        {
            _connectionManager=connectionManager;
        }
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.AddConnection(userId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveConnection(userId, Context.ConnectionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task MarkAsRead(int notificationId)
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // Mark notification as read in database
            await Clients.Caller.SendAsync("NotificationRead", notificationId);
        }
    }
}