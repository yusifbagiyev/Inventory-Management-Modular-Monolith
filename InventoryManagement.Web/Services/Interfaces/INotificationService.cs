using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, int? limit = null);
        Task<Models.ViewModels.NotificationListViewModel> GetPageAsync(bool unreadOnly, string? type, int pageNumber, int pageSize);
        Task<int> GetUnreadCountAsync();
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync();
    }
}