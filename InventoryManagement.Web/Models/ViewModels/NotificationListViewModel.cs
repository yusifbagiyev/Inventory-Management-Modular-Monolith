using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class NotificationListViewModel
    {
        public List<NotificationDto> Notifications { get; set; } = new();
    }
}