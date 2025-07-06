using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false);
        Task<int> GetUnreadCountAsync();
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync();
    }
}