using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Services
{
    /// <summary>Pushes notifications to the browser, with each connection in its user's group.</summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                await Clients.Caller.SendAsync("ConnectionEstablished");

                _logger.LogDebug("User {UserId} connected to the notification hub ({ConnectionId})", userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }
    }
}
