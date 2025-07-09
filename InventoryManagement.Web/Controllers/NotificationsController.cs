using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(string? status = null, string? type = null)
        {
            var notifications = await _notificationService.GetNotificationsAsync(status == "unread");

            var model = new NotificationListViewModel
            {
                Notifications = notifications
                    .Where(n => type == null || n.Type == type)
                    .ToList()
            };

            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter = type;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            await _notificationService.MarkAsReadAsync(notificationId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _notificationService.MarkAllAsReadAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _notificationService.GetUnreadCountAsync();
            return Json(count);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            var notifications = await _notificationService.GetNotificationsAsync(false);
            var recent = notifications
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToList();

            return Json(recent);
        }
    }
}