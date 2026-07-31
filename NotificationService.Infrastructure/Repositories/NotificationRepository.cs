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

        public Task DeleteAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            _context.Notifications.Remove(notification);
            return Task.CompletedTask;
        }
    }
}