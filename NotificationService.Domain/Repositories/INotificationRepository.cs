using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false, CancellationToken cancellationToken = default, int? limit = null);
        /// <summary>One page of the user's notifications, newest first, and how many match in all.</summary>
        Task<(List<Notification> Items, int Total)> GetPageAsync(int userId, bool unreadOnly, string? type, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
        /// <summary>How many of the user's notifications there are, optionally only unread ones and of one type.</summary>
        Task<int> CountAsync(int userId, bool unreadOnly, string? type, CancellationToken cancellationToken = default);
        /// <summary>The notification types the user has, for the filter.</summary>
        Task<List<string>> GetTypesAsync(int userId, CancellationToken cancellationToken = default);
        Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);
        Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
        /// <summary>Marks all the user's unread notifications as read in one UPDATE and returns the count.</summary>
        Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
        /// <summary>Deletes all users' notifications about one approval request and returns the count.</summary>
        Task<int> DeleteByApprovalRequestAsync(int approvalRequestId, CancellationToken cancellationToken = default);
    }
}