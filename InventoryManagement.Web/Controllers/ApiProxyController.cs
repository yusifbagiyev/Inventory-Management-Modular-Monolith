using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    [Route("api-proxy")]
    public class ApiProxyController : ControllerBase
    {
        private readonly IApiService _apiService;
        private readonly ILogger<ApiProxyController> _logger;

        public ApiProxyController(IApiService apiService, ILogger<ApiProxyController> logger)
        {
            _apiService = apiService;
            _logger = logger;
        }

        /// <summary>
        /// Proxy GET requests to the API Gateway
        /// This keeps tokens server-side and never exposes them to the browser
        /// </summary>
        [HttpGet("{*path}")]
        public async Task<IActionResult> Get(string path)
        {
            try
            {
                // The ApiService already has access to the session token
                // No token needs to be sent from the client
                var result = await _apiService.GetAsync<object>(path);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying GET request to {Path}", path);
                return StatusCode(500, new { error = "An error occurred processing your request" });
            }
        }

        /// <summary>
        /// Proxy POST requests to the API Gateway
        /// </summary>
        [HttpPost("{*path}")]
        public async Task<IActionResult> Post(string path, [FromBody] object data)
        {
            try
            {
                var result = await _apiService.PostAsync<object, object>(path, data);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying POST request to {Path}", path);
                return StatusCode(500, new { error = "An error occurred processing your request" });
            }
        }
    }
}