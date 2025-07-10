using ApprovalService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IMessagePublisher _messagePublisher;
        private readonly ILogger<TestController> _logger;

        public TestController(IMessagePublisher messagePublisher, ILogger<TestController> logger)
        {
            _messagePublisher = messagePublisher;
            _logger = logger;
        }

        [HttpGet("test-rabbitmq")]
        public async Task<IActionResult> TestRabbitMQ()
        {
            try
            {
                var testEvent = new
                {
                    RequestId = 999,
                    RequestType = "Test",
                    RequestedById = 1,
                    RequestedByName = "Test User",
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Sending test message to RabbitMQ...");
                await _messagePublisher.PublishAsync(testEvent, "approval.request.created");

                return Ok(new { success = true, message = "Test message sent to RabbitMQ" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ test failed");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
