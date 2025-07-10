using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Events;
using NotificationService.Infrastructure.Services;

namespace NotificationService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly INotificationSender _notificationSender;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IUserService _userService;

        public TestController(
            INotificationSender notificationSender, 
            IHubContext<NotificationHub> hubContext,
            IUserService userService)
        {
            _notificationSender = notificationSender;
            _hubContext = hubContext;
            _userService = userService;
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

        [HttpGet("test-approval-notification")]
        [AllowAnonymous]
        public async Task<IActionResult> TestApprovalNotification()
        {
            try
            {
                // Simulate an approval request created event
                var testEvent = new ApprovalRequestCreatedEvent
                {
                    RequestId = 999,
                    RequestType = "product.create",
                    RequestedById = 1,
                    RequestedByName = "Test Manager",
                    CreatedAt = DateTime.UtcNow
                };

                // Get admin users
                var adminUsers = await _userService.GetUsersAsync("Admin");

                foreach (var admin in adminUsers)
                {
                    await _notificationSender.SendToUserAsync(
                        admin.Id,
                        "ApprovalRequest",
                        "Test Approval Request",
                        "This is a test notification for approval request",
                        new { approvalRequestId = 999 }
                    );
                }

                return Ok(new
                {
                    success = true,
                    message = $"Test notifications sent to {adminUsers.Count} admin users"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}