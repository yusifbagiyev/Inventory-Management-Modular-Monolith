using System.Security.Claims;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services.Interfaces;
using NotificationService.Application.Interfaces;

namespace InventoryManagement.Web.Services
{
    /// <summary>The signed-in user's notifications, backed in-process by the notification module.</summary>
    public class NotificationService : INotificationService
    {
        private readonly INotificationInbox _inbox;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationService(INotificationInbox inbox, IHttpContextAccessor httpContextAccessor)
        {
            _inbox = inbox;
            _httpContextAccessor = httpContextAccessor;
        }

        private int UserId => int.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false)
            => ModelMapper.MapList<NotificationDto>(await _inbox.GetAsync(UserId, unreadOnly));

        public Task<int> GetUnreadCountAsync() => _inbox.GetUnreadCountAsync(UserId);

        public Task MarkAsReadAsync(int notificationId) => _inbox.MarkAsReadAsync(UserId, notificationId);

        public Task MarkAllAsReadAsync() => _inbox.MarkAllAsReadAsync(UserId);
    }
}
