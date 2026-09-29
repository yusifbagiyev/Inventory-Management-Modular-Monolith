using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Repositories;

namespace NotificationService.Infrastructure.Services
{
    public class NotificationInbox : INotificationInbox
    {
        private readonly INotificationRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public NotificationInbox(INotificationRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<NotificationDto>> GetAsync(int userId, bool unreadOnly = false, int? limit = null, CancellationToken cancellationToken = default)
        {
            var notifications = await _repository.GetByUserIdAsync(userId, unreadOnly, cancellationToken, limit);
            return notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                Data = n.Data,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            }).ToList();
        }

        public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
            => _repository.GetUnreadCountAsync(userId, cancellationToken);

        public async Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
        {
            var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
            if (notification == null || notification.UserId != userId)
                return false;

            notification.MarkAsRead();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        public Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
            => _repository.MarkAllAsReadAsync(userId, cancellationToken);
    }
}
