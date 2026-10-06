using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RouteService.Infrastructure.Data;
using SharedServices.Contracts;

namespace RouteService.Infrastructure.Services
{
    /// <summary>Marks the WhatsApp messages a restart dropped from the in-memory outbox as failed, so they can be sent again.</summary>
    public sealed class InterruptedWhatsAppMessages : IHostedService
    {
        public const string Error = "The application restarted before the message was sent.";

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<InterruptedWhatsAppMessages> _logger;

        public InterruptedWhatsAppMessages(IServiceScopeFactory scopes, ILogger<InterruptedWhatsAppMessages> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Only messages queued before this start, since the outbox of this process begins empty
            var startedAt = DateTime.Now;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
                var count = await context.InventoryRoutes
                    .Where(r => r.WhatsAppStatus == WhatsAppStatus.Queued && r.WhatsAppAt < startedAt)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.WhatsAppStatus, WhatsAppStatus.Failed)
                        .SetProperty(r => r.WhatsAppError, Error)
                        .SetProperty(r => r.WhatsAppAt, startedAt), cancellationToken);
                if (count > 0)
                    _logger.LogWarning("{Count} queued WhatsApp messages were lost in a restart and are marked as failed", count);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The app still starts, the messages only stay marked as queued
                _logger.LogError(ex, "Could not mark the WhatsApp messages lost in a restart as failed");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
