using System.Security.Claims;
using InventoryManagement.Web.Localization;
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

        /// <summary>Notifications are stored in English once for all recipients and translated when shown.</summary>
        public async Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, int? limit = null)
            => Translate(ModelMapper.MapList<NotificationDto>(await _inbox.GetAsync(UserId, unreadOnly, limit)));

        public async Task<Models.ViewModels.NotificationListViewModel> GetPageAsync(bool unreadOnly, string? type, int pageNumber, int pageSize)
        {
            var page = await _inbox.GetPageAsync(UserId, unreadOnly, type, pageNumber, pageSize);
            return new Models.ViewModels.NotificationListViewModel
            {
                Notifications = Translate(ModelMapper.MapList<NotificationDto>(page.Items)),
                TotalCount = page.TotalCount,
                AllCount = page.AllCount,
                UnreadCount = page.UnreadCount,
                TotalUnreadCount = page.TotalUnreadCount,
                Types = page.Types,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private static List<NotificationDto> Translate(IEnumerable<NotificationDto> items)
            => items.Select(n => n with
                {
                    Title = JsonStringLocalizer.TranslateMessage(n.Title),
                    Message = JsonStringLocalizer.TranslateMessage(n.Message)
                })
                .ToList();

        public Task<int> GetUnreadCountAsync() => _inbox.GetUnreadCountAsync(UserId);

        public Task MarkAsReadAsync(int notificationId) => _inbox.MarkAsReadAsync(UserId, notificationId);

        public Task MarkAllAsReadAsync() => _inbox.MarkAllAsReadAsync(UserId);
    }
}
