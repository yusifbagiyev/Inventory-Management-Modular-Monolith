using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Localization;
using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Services;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SharedServices.Identity;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class NotificationsController : BaseController
    {
        private readonly INotificationService _notificationService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public NotificationsController(INotificationService notificationService, IStringLocalizer<SharedResource> localizer, ILogger<NotificationsController> logger)
            :base(logger)
        {
            _notificationService = notificationService;
            _localizer = localizer;
        }

        /// <summary>Every notification type the user can receive, plus any older type still in their list.</summary>
        private List<string> FilterTypes(IEnumerable<string> held)
        {
            var types = new List<string>();
            if (User.HasPermission(AllPermissions.ProductView)) types.Add("ProductUpdate");
            if (User.HasPermission(AllPermissions.RouteView)) types.Add("RouteUpdate");
            if (User.HasPermission(AllPermissions.ApprovalDecide)) types.Add("ApprovalRequest");
            types.Add("ApprovalResponse");
            types.AddRange(held.Where(t => !string.IsNullOrEmpty(t) && !types.Contains(t)));
            return types;
        }

        public async Task<IActionResult> Index(string? status = null, string? type = null, int pageNumber = 1, int pageSize = 30)
        {
            try
            {
                pageSize = Math.Clamp(pageSize, 1, 100);
                var model = await _notificationService.GetPageAsync(status == "unread", string.IsNullOrEmpty(type) ? null : type, Math.Max(1, pageNumber), pageSize);
                model.Types = FilterTypes(model.Types);

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
                // Each item carries where it leads, so the bell opens the same record as the Notifications page
                var recent = await _notificationService.GetNotificationsAsync(unreadOnly: true, limit: 5);
                return Json(recent.Select(n => new
                {
                    n.Id, n.Type, n.Title, n.Message, n.Data, n.IsRead, n.CreatedAt,
                    Link = NotificationLinks.For(n, User, _localizer)?.Url
                }));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get recent notifications");
                return Json(new List<NotificationDto>());
            }
        }
    }
}