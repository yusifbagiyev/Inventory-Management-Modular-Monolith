using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class NotificationsController : BaseController
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
            :base(logger)
        {
            _notificationService = notificationService;
        }

        /// <summary>Paged, newest first; the tabs (all / unread) and the type filter are query parameters (ListNav updates the list in place).</summary>
        public async Task<IActionResult> Index(string? status = null, string? type = null, int pageNumber = 1, int pageSize = 30)
        {
            try
            {
                pageSize = Math.Clamp(pageSize, 1, 100);
                var model = await _notificationService.GetPageAsync(status == "unread", string.IsNullOrEmpty(type) ? null : type, Math.Max(1, pageNumber), pageSize);

                ViewBag.StatusFilter = status;
                ViewBag.TypeFilter = type;

                return View(model);
            }
            catch (Exception ex)
            {
                return HandleException(ex, new NotificationListViewModel());
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead([FromBody] int notificationId)
        {
            try
            {
                await _notificationService.MarkAsReadAsync(notificationId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to mark notification as read");
                return Json(new { success = false, error = ex.Message });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                await _notificationService.MarkAllAsReadAsync();

                if (IsAjaxRequest())
                {
                    return AjaxResponse(true, "All notifications marked as read");
                }

                TempData["Success"] = Tr("All notifications marked as read");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var count = await _notificationService.GetUnreadCountAsync();
                return Json(count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get unread notification count");
                return Json(0);
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications()
        {
            try
            {
                return Json(await _notificationService.GetNotificationsAsync(unreadOnly: true, limit: 5));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get recent notifications");
                return Json(new List<NotificationDto>());
            }
        }
    }
}