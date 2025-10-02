using InventoryManagement.Web.Services.Interfaces;

namespace InventoryManagement.Web.Services
{
    public class TokenRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenRefreshBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

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
                    await Task.Delay(_checkInterval, stoppingToken);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();

                        // Only refresh if there's an active HTTP context (user is active)
                        if (httpContextAccessor?.HttpContext != null)
                        {
                            var tokenManager = scope.ServiceProvider.GetRequiredService<ITokenManager>();

                            // This will check and refresh if needed
                            await tokenManager.GetValidTokenAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in token refresh background service");
                }
            }
        }
    }
}