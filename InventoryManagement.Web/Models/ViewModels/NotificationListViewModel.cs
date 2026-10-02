using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class NotificationListViewModel
    {
        public List<NotificationDto> Notifications { get; set; } = new();
        /// <summary>Matches for the current tab and type, used for paging.</summary>
        public int TotalCount { get; set; }
        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public List<string> Types { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }
}