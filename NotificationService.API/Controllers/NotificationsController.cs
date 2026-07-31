using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Repositories;

namespace NotificationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public NotificationsController(INotificationRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetMyNotifications([FromQuery] bool unreadOnly = false)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == 0)
                return Ok(new List<NotificationDto>());

            var notifications = await _repository.GetByUserIdAsync(userId, unreadOnly);
            return Ok(notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                Data = n.Data,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            }));
        }



        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetRecentNotifications()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == 0)
                return Ok(new List<NotificationDto>());

            // Cap the fetch in SQL - this used to load the user's whole notification history
            // and then keep only the newest five in memory.
            var notifications = await _repository.GetByUserIdAsync(userId, false, limit: 5);
            var recent = notifications
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    Data = n.Data,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                });

            return Ok(recent);
        }




        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var count = await _repository.GetUnreadCountAsync(userId);
            return Ok(count);
        }




        [HttpPost("mark-as-read")]
        public async Task<IActionResult> MarkAsRead(MarkAsReadDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var notification = await _repository.GetByIdAsync(dto.NotificationId);
                if (notification == null)
                    return NotFound(new { message = "Notification not found" });

                // Verify the notification belongs to the user
                if (notification.UserId != userId)
                    return Forbid();

                notification.MarkAsRead();
                await _unitOfWork.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch
            {
                return BadRequest(new { message = "Failed to mark notification as read" });
            }
        }



        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Set-based UPDATE. The old load-then-mutate path used AsNoTracking rows, so
                // SaveChanges never persisted the change and "mark all read" silently no-op'd.
                var count = await _repository.MarkAllAsReadAsync(userId);

                return Ok(new { success = true, count });
            }
            catch
            {
                return BadRequest(new { message = "Failed to mark all notifications as read" });
            }
        }
    }
}