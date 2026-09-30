using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private const int RecentCount = 5;

        private readonly INotificationInbox _inbox;

        public NotificationsController(INotificationInbox inbox)
        {
            _inbox = inbox;
        }

        private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetMyNotifications([FromQuery] bool unreadOnly = false)
            => Ok(await _inbox.GetAsync(CurrentUserId, unreadOnly));

        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetRecentNotifications()
            => Ok(await _inbox.GetAsync(CurrentUserId, limit: RecentCount));

        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
            => Ok(await _inbox.GetUnreadCountAsync(CurrentUserId));

        [HttpPost("mark-as-read")]
        public async Task<IActionResult> MarkAsRead(MarkAsReadDto dto)
            => await _inbox.MarkAsReadAsync(CurrentUserId, dto.NotificationId)
                ? Ok(new { success = true })
                : NotFound(new { message = "Notification not found" });

        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
            => Ok(new { success = true, count = await _inbox.MarkAllAsReadAsync(CurrentUserId) });
    }
}
