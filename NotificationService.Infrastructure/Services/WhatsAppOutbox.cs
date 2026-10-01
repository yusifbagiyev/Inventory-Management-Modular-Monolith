using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using SharedServices.Contracts;
using SharedServices.Events;
using SharedServices.Storage;

namespace NotificationService.Infrastructure.Services
{
    /// <summary>One WhatsApp group message waiting to go out; RouteId set for a completed transfer.</summary>
    public sealed record WhatsAppJob(string Message, string? ImageUrl, string FallbackFileName, int InventoryCode, int? RouteId);

    /// <summary>
    /// WhatsApp messages go out one at a time from here (WhatsAppOutboxWorker), so their pace can be
    /// kept: the WaSender account allows one message every 5 seconds and refused the second of two
    /// transfers completed 3 seconds apart. Lost on restart, like the background queue.
    /// </summary>
    public sealed class WhatsAppOutbox
    {
        private readonly Channel<WhatsAppJob> _channel = Channel.CreateUnbounded<WhatsAppJob>();
        private readonly IConfiguration _configuration;

        public WhatsAppOutbox(IConfiguration configuration) => _configuration = configuration;

        public string? GroupId => _configuration.GetValue("WhatsApp:Enabled", true) ? _configuration["WhatsApp:DefaultGroupId"] : null;

        public bool Enabled => !string.IsNullOrEmpty(GroupId);

        public void Enqueue(WhatsAppJob job) => _channel.Writer.TryWrite(job);

        internal ChannelReader<WhatsAppJob> Reader => _channel.Reader;
    }

    /// <summary>
    /// Sends the outbox in order, at least WhatsApp:MinIntervalSeconds (5.5) apart. A rate-limited
    /// message waits as long as the service asks and is tried again (up to 5 times); other errors
    /// get two more tries. A transfer's outcome is stored on its route (Sent / Failed + reason),
    /// where a failed one can be sent again.
    /// </summary>
    public sealed class WhatsAppOutboxWorker : BackgroundService
    {
        private const int MaxAttempts = 5;
        private const int MaxOtherErrors = 3;

        private readonly WhatsAppOutbox _outbox;
        private readonly IServiceScopeFactory _scopes;
        private readonly ImageStorage _images;
        private readonly ILogger<WhatsAppOutboxWorker> _logger;
        private readonly TimeSpan _minInterval;
        private DateTime _lastSentUtc = DateTime.MinValue;

        public WhatsAppOutboxWorker(WhatsAppOutbox outbox, IServiceScopeFactory scopes, ImageStorage images,
            IConfiguration configuration, ILogger<WhatsAppOutboxWorker> logger)
        {
            _outbox = outbox;
            _scopes = scopes;
            _images = images;
            _logger = logger;
            _minInterval = TimeSpan.FromSeconds(configuration.GetValue("WhatsApp:MinIntervalSeconds", 5.5));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _outbox.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await SendAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WhatsApp outbox failed for inventory code {Code}", job.InventoryCode);
                    await RecordAsync(job, WhatsAppStatus.Failed, ex.Message, stoppingToken);
                }
            }
        }

        private async Task SendAsync(WhatsAppJob job, CancellationToken cancellationToken)
        {
            var groupId = _outbox.GroupId;
            if (string.IsNullOrEmpty(groupId)) return;   // switched off meanwhile

            var image = await _images.ReadAsync(job.ImageUrl, cancellationToken);
            var fileName = Path.GetFileName(job.ImageUrl) ?? job.FallbackFileName;
            string? uploaded = null, lastError = null;
            var otherErrors = 0;

            await using var scope = _scopes.CreateAsyncScope();
            var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var wait = _lastSentUtc + _minInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, cancellationToken);

                var result = await whatsApp.SendAsync(groupId, job.Message, image, fileName, uploaded, cancellationToken);
                _lastSentUtc = DateTime.UtcNow;
                uploaded ??= result.UploadedImageUrl;

                if (result.Success)
                {
                    await RecordAsync(job, WhatsAppStatus.Sent, null, cancellationToken);
                    return;
                }

                lastError = result.Error;
                if (result.RateLimited)
                {
                    await Task.Delay((result.RetryAfter ?? _minInterval) + TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }
                if (++otherErrors >= MaxOtherErrors) break;
                await Task.Delay(TimeSpan.FromSeconds(10 * otherErrors), cancellationToken);
            }

            _logger.LogWarning("WhatsApp message for inventory code {Code} was not delivered: {Error}", job.InventoryCode, lastError);
            await RecordAsync(job, WhatsAppStatus.Failed, lastError, cancellationToken);
        }

        private async Task RecordAsync(WhatsAppJob job, string status, string? error, CancellationToken cancellationToken)
        {
            if (job.RouteId is not int routeId) return;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRouteWhatsAppStatus>().SetAsync(routeId, status, error, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not record WhatsApp status {Status} for route {RouteId}", status, routeId);
            }
        }
    }

    /// <summary>Queues WhatsApp messages: product created, transfer completed, and resends from the route pages.</summary>
    public sealed class WhatsAppRouteNotifier : IWhatsAppRouteNotifier
    {
        private readonly WhatsAppOutbox _outbox;
        private readonly IWhatsAppService _whatsApp;

        public WhatsAppRouteNotifier(WhatsAppOutbox outbox, IWhatsAppService whatsApp)
        {
            _outbox = outbox;
            _whatsApp = whatsApp;
        }

        public bool Enabled => _outbox.Enabled;

        public Task QueueRouteCompletedAsync(RouteCompletedEvent e, CancellationToken cancellationToken = default)
        {
            Queue(new WhatsAppProductNotification
            {
                ProductId = e.ProductId,
                InventoryCode = e.InventoryCode,
                Model = e.Model,
                Vendor = e.Vendor,
                CategoryName = e.CategoryName,
                FromDepartmentName = e.FromDepartmentName,
                FromWorker = e.FromWorker,
                ToDepartmentName = e.ToDepartmentName,
                ToWorker = e.ToWorker,
                CreatedAt = e.CompletedAt,
                Notes = e.Notes,
                NotificationType = "transferred",
                ImageUrl = e.ImageUrl
            }, $"route_{e.InventoryCode}.jpg", e.RouteId);
            return Task.CompletedTask;
        }

        public void Queue(WhatsAppProductNotification notification, string fallbackFileName, int? routeId)
        {
            if (!_outbox.Enabled) return;
            _outbox.Enqueue(new WhatsAppJob(_whatsApp.FormatNotification(notification), notification.ImageUrl,
                fallbackFileName, notification.InventoryCode, routeId));
        }
    }
}
