using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;

namespace NotificationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly INotificationSender _notificationSender;
        private readonly IHubContext<NotificationHub> _hubContext;

        public TestController(INotificationSender notificationSender, IHubContext<NotificationHub> hubContext)
        {
            _notificationSender = notificationSender;
            _hubContext = hubContext;
        }

        [HttpGet("test-notification/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> TestNotification(int userId)
        {
            try
            {
                // Direct SignalR test
                await _hubContext.Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", new
                {
                    Id = 999,
                    Type = "Test",
                    Title = "Test Notification",
                    Message = "This is a test notification",
                    CreatedAt = DateTime.UtcNow
                });

                return Ok(new { success = true, message = $"Test notification sent to user {userId}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
