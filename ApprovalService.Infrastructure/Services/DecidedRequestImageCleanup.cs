using ApprovalService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApprovalService.Infrastructure.Services
{
    /// <summary>Drops the uploaded image bytes that requests decided before this clean-up existed still store.</summary>
    public sealed class DecidedRequestImageCleanup : BackgroundService
    {
        private const int BatchSize = 50;

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DecidedRequestImageCleanup> _logger;

        public DecidedRequestImageCleanup(IServiceScopeFactory scopes, ILogger<DecidedRequestImageCleanup> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Let startup and the warm-up queries go first
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                var cleaned = 0;
                var lastId = 0;
                while (!stoppingToken.IsCancellationRequested)
                {
                    await using var scope = _scopes.CreateAsyncScope();   // DbSession only disposes asynchronously
                    var repository = scope.ServiceProvider.GetRequiredService<IApprovalRequestRepository>();

                    var ids = await repository.GetLargeDecidedIdsAsync(lastId, BatchSize, stoppingToken);
                    if (ids.Count == 0) break;

                    // One request per statement keeps each one short however many photos it holds
                    foreach (var id in ids)
                    {
                        if (await repository.DropImageDataAsync(id, stoppingToken))
                            cleaned++;
                    }
                    lastId = ids[^1];
                }

                if (cleaned > 0)
                    _logger.LogInformation("Removed the stored image bytes of {Count} decided approval requests", cleaned);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The next start continues, since cleaned requests are no longer large
            }
            catch (Exception ex)
            {
                // Only saves space, so a failure must never take the host down
                _logger.LogWarning(ex, "Clean-up of decided approval requests' image bytes failed");
            }
        }
    }
}
