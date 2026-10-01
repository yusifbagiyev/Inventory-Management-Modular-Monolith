using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false, CancellationToken cancellationToken = default, int? limit = null);
        /// <summary>One page of the user's notifications, newest first, and how many match in all.</summary>
        Task<(List<Notification> Items, int Total)> GetPageAsync(int userId, bool unreadOnly, string? type, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        Task<int> CountAsync(int userId, CancellationToken cancellationToken = default);
        /// <summary>The notification types the user has, for the filter.</summary>
        Task<List<string>> GetTypesAsync(int userId, CancellationToken cancellationToken = default);
        Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);
        Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
        /// <summary>Marks every unread notification for the user as read in a single UPDATE. Returns the affected count.</summary>
        Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
        /// <summary>Deletes every user's notifications about one approval request. Returns the deleted count.</summary>
        Task<int> DeleteByApprovalRequestAsync(int approvalRequestId, CancellationToken cancellationToken = default);
    }
}