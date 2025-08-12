using NotificationService.Application.DTOs;

namespace NotificationService.Application.Interfaces
{
    public interface IWhatsAppService
    {
        Task<bool> SendGroupMessageAsync(string groupId, string message);
        string FormatProductNotification(WhatsAppProductNotification notification);
    }
    
}