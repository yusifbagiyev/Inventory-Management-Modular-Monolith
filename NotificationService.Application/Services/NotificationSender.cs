using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Services
{
    public class NotificationSender:INotificationSender
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly INotificationRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public NotificationSender(
            IHubContext<NotificationHub> hubContext,
            INotificationRepository repository,
            IUnitOfWork unitOfWork)
        {
            _hubContext= hubContext;
            _repository= repository;
            _unitOfWork= unitOfWork;
        }

        public async Task SendToUserAsync(int userId,string type,string title,string message,object? data = null)
        {
            //Save to the database
            var notification = new Notification(
                userId,
                type,
                title,
                message,
                data != null ? System.Text.Json.JsonSerializer.Serialize(data) : null);

            await _repository.AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            //Send via SignalR
            await _hubContext.Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", new
            {
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.CreatedAt,
                Data = data
            });
        }
        public async Task SendToRoleAsync(string role, string type, string title, string message, object? data = null)
        {
            // Get all users in role and send notifications
            // This would require integration with Identity service
            await _hubContext.Clients.Group($"role-{role}").SendAsync("ReceiveNotification", new
            {
                Type = type,
                Title = title,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                Data = data
            });
        }
    }
}