namespace NotificationService.Application.Interfaces
{
    public interface INotificationSender
    {
        Task SendToUserAsync(int userId, string type, string title, string message, object? data = null);
        Task SendToRoleAsync(string role, string type, string title, string message, object? data = null);
    }
}