using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class TokenRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenRefreshBackgroundService> _logger;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(10);

        public TokenRefreshBackgroundService(
           IServiceProvider serviceProvider,
           ILogger<TokenRefreshBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();
                    var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

                    // Only refresh if there's an active session
                    if (httpContextAccessor.HttpContext?.Session != null)
                    {
                        var currentToken = httpContextAccessor.HttpContext.Session.GetString("JwtToken");
                        if (!string.IsNullOrEmpty(currentToken))
                        {
                            var validToken = await tokenManager.GetValidTokenAsync();

                            if (string.IsNullOrEmpty(validToken))
                            {
                                _logger.LogWarning("Token refresh failed in background service");
                            }
                            else
                            {
                                _logger.LogInformation("Token refreshed successfully in background");
                            }
                        }
                    }
                    await Task.Delay(_refreshInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in token refresh background service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }
    }
}
