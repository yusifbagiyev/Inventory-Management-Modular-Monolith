using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    /// <summary>A user's stored notifications, with every method scoped to the given user.</summary>
    public interface INotificationInbox
    {
        Task<IReadOnlyList<NotificationDto>> GetAsync(int userId, bool unreadOnly = false, int? limit = null, CancellationToken cancellationToken = default);

        Task<NotificationPageDto> GetPageAsync(int userId, bool unreadOnly, string? type, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

        /// <returns>False when the notification does not exist or belongs to another user.</returns>
        Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);

        Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
    }
}
