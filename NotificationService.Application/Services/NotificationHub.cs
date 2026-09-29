using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Services
{
    /// <summary>
    /// Pushes notifications to the browser. Each connection joins "user-{id}" and "role-{role}"
    /// groups so the dispatcher can target a user or every holder of a role.
    /// </summary>
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
                var userGroup = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);

                var roleGroups = (Context.User?.FindAll(ClaimTypes.Role) ?? [])
                    .Select(c => $"role-{c.Value}")
                    .ToList();
                foreach (var roleGroup in roleGroups)
                    await Groups.AddToGroupAsync(Context.ConnectionId, roleGroup);

                await Clients.Caller.SendAsync("ConnectionEstablished", new
                {
                    connectionId = Context.ConnectionId,
                    userId,
                    userGroup,
                    roleGroups,
                    timestamp = DateTime.Now,
                    message = "Connected successfully"
                });

                _logger.LogDebug("User {UserId} connected to the notification hub ({ConnectionId})", userId, Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }
    }
}
