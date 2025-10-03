using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<ConnectionManager> _logger;

        public ConnectionManager(
            IHttpContextAccessor httpContextAccessor,
            ITokenManager tokenManager,
            ILogger<ConnectionManager> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _tokenManager = tokenManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets a token for SignalR connection without exposing it to the client
        /// The client will call this endpoint to get a token just-in-time
        /// </summary>
        public async Task<string> GetSignalRTokenAsync()
        {
            try
            {
                // Get valid token from server-side session
                var token = await _tokenManager.GetValidTokenAsync();

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No valid token available for SignalR connection");
                    throw new UnauthorizedAccessException("No valid authentication token");
                }

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR token");
                throw;
            }
        }
    }
}