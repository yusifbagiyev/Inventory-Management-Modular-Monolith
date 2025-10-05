using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/token")]
    public class TokenController : ControllerBase
    {
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<TokenController> _logger;

        public TokenController(ITokenManager tokenManager, ILogger<TokenController> logger)
        {
            _tokenManager = tokenManager;
            _logger = logger;
        }

        /// <summary>
        /// Provides the current JWT token for client-side use (SignalR, direct API calls)
        /// SECURITY: Only returns token if user is authenticated and token is valid
        /// Token is refreshed automatically if needed
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentToken()
        {
            try
            {
                // This will auto-refresh if needed
                var token = await _tokenManager.GetValidTokenAsync();

                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { error = "No valid token available" });
                }

                // Return minimal response - just the token
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current token");
                return StatusCode(500, new { error = "Failed to retrieve token" });
            }
        }

        /// <summary>
        /// Validates if current token is still valid
        /// Used for health checks
        /// </summary>
        [HttpGet("validate")]
        public async Task<IActionResult> ValidateToken()
        {
            try
            {
                var token = await _tokenManager.GetValidTokenAsync();
                var isValid = !string.IsNullOrEmpty(token);

                return Ok(new { isValid, timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return Ok(new { isValid = false, timestamp = DateTime.Now });
            }
        }
    }
}