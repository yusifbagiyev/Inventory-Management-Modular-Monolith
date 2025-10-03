using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class ConnectionController : ControllerBase
    {
        private readonly IConnectionManager _connectionManager;

        public ConnectionController(IConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// Provides a SignalR token without exposing it in the page
        /// The client calls this just before connecting to SignalR
        /// </summary>
        [HttpGet("signalr-token")]
        public async Task<IActionResult> GetSignalRToken()
        {
            try
            {
                var token = await _connectionManager.GetSignalRTokenAsync();
                return Ok(new { token });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}