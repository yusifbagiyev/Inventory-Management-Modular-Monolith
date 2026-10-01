using InventoryManagement.Web.Models.DTOs;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class NotificationListViewModel
    {
        /// <summary>This page's notifications, newest first.</summary>
        public List<NotificationDto> Notifications { get; set; } = new();
        /// <summary>How many match the tab and type (for the pages); the tab counts; the types for the filter.</summary>
        public int TotalCount { get; set; }
        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public List<string> Types { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }
}