using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false, CancellationToken cancellationToken = default, int? limit = null);
        Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);
        Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
        /// <summary>Marks every unread notification for the user as read in a single UPDATE. Returns the affected count.</summary>
        Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
        Task DeleteAsync(Notification notification, CancellationToken cancellationToken = default);
    }
}