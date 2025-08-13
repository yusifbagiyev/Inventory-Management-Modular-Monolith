using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    public interface IWhatsAppService
    {
        Task<bool> SendGroupMessageAsync(string groupId, string message);
        Task<bool> SendGroupMessageWithImageDataAsync(string groupId, string message, byte[] imageData, string fileName);
        string FormatNotification(WhatsAppProductNotification notification);
    }
}