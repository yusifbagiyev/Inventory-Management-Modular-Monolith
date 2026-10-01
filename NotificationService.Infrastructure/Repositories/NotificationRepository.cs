using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.Data;

namespace NotificationService.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly NotificationDbContext _context;

        public NotificationRepository(NotificationDbContext context)
        {
            _context = context;
        }

        public async Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IEnumerable<Notification>> GetByUserIdAsync(int userId, bool unreadOnly = false, CancellationToken cancellationToken = default, int? limit = null)
        {
            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            query = query.OrderByDescending(n => n.CreatedAt);

            // Cap in SQL. Callers that render only the newest few used to pull the user's entire
            // notification history and then Take(n) in memory.
            if (limit.HasValue)
                query = query.Take(limit.Value);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task<(List<Notification> Items, int Total)> GetPageAsync(int userId, bool unreadOnly, string? type, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);
            if (unreadOnly)
                query = query.Where(n => !n.IsRead);
            if (!string.IsNullOrEmpty(type))
                query = query.Where(n => n.Type == type);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                .Skip((Math.Max(1, pageNumber) - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, total);
        }

        public Task<int> CountAsync(int userId, CancellationToken cancellationToken = default)
            => _context.Notifications.CountAsync(n => n.UserId == userId, cancellationToken);

        public Task<List<string>> GetTypesAsync(int userId, CancellationToken cancellationToken = default)
            => _context.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .Select(n => n.Type).Distinct().OrderBy(t => t)
                .ToListAsync(cancellationToken);

        public async Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            await _context.Notifications.AddAsync(notification, cancellationToken);
            return notification;
        }

        public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            _context.Entry(notification).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
        }

        // Bulk mark-as-read. A set-based UPDATE, so it does not depend on change tracking - the
        // previous controller loaded these rows with AsNoTracking(), mutated them, and SaveChanges
        // persisted nothing. Mirrors Notification.MarkAsRead() (IsRead + ReadAt).
        public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, DateTime.Now), cancellationToken);
        }

        public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
            => _context.Notifications.AddRangeAsync(notifications, cancellationToken);

        public Task<int> DeleteByApprovalRequestAsync(int approvalRequestId, CancellationToken cancellationToken = default)
        {
            // Data is compact JSON written by NotificationDispatcher, so the id is followed by ',' or
            // '}'. Matching the delimiter keeps request 1 from also deleting requests 10-19, 100...
            var withComma = $"\"approvalRequestId\":{approvalRequestId},";
            var atEnd = $"\"approvalRequestId\":{approvalRequestId}}}";
            return _context.Notifications
                .Where(n => n.Data != null && (n.Data.Contains(withComma) || n.Data.Contains(atEnd)))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}