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
    /// <summary>One WhatsApp group message waiting to go out, with RouteId set for the route that shows its outcome.</summary>
    public sealed record WhatsAppJob(string Message, string? ImageUrl, int InventoryCode, int? RouteId);

    /// <summary>In-memory queue that lets WhatsApp messages out one at a time, since WaSender allows one call every 5 seconds.</summary>
    public sealed class WhatsAppOutbox
    {
        private readonly Channel<WhatsAppJob> _channel = Channel.CreateUnbounded<WhatsAppJob>();
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private DateTime _lastCallUtc = DateTime.MinValue;

        public WhatsAppOutbox(IConfiguration configuration)
        {
            _configuration = configuration;
            MinInterval = TimeSpan.FromSeconds(configuration.GetValue("WhatsApp:MinIntervalSeconds", 5.5));
        }

        public string? GroupId => _configuration.GetValue("WhatsApp:Enabled", true) ? _configuration["WhatsApp:DefaultGroupId"] : null;

        public bool Enabled => !string.IsNullOrEmpty(GroupId);

        public TimeSpan MinInterval { get; }

        public void Enqueue(WhatsAppJob job) => _channel.Writer.TryWrite(job);

        internal ChannelReader<WhatsAppJob> Reader => _channel.Reader;

        /// <summary>Runs one WaSender call at least MinInterval after the previous one, so sends and deletes share the account's limit.</summary>
        public async Task<T> PacedAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var wait = _lastCallUtc + MinInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, cancellationToken);
                try
                {
                    return await call();
                }
                finally
                {
                    _lastCallUtc = DateTime.UtcNow;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    /// <summary>Sends the outbox in order and at least WhatsApp:MinIntervalSeconds apart, retrying failed messages.</summary>
    public sealed class WhatsAppOutboxWorker : BackgroundService
    {
        private const int MaxAttempts = 5;
        private const int MaxOtherErrors = 3;
        private const int RecordAttempts = 3;

        /// <summary>Stored on a route whose message was queued or being sent when the app stopped.</summary>
        public const string InterruptedError = "Sending was interrupted by a restart. Check the group before sending again.";

        private readonly WhatsAppOutbox _outbox;
        private readonly IServiceScopeFactory _scopes;
        private readonly ImageStorage _images;
        private readonly ILogger<WhatsAppOutboxWorker> _logger;

        public WhatsAppOutboxWorker(WhatsAppOutbox outbox, IServiceScopeFactory scopes, ImageStorage images,
            ILogger<WhatsAppOutboxWorker> logger)
        {
            _outbox = outbox;
            _scopes = scopes;
            _images = images;
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // The outbox starts empty, so a message still marked queued was lost with the previous process
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var interrupted = await scope.ServiceProvider.GetRequiredService<IRouteWhatsAppStatus>()
                    .FailQueuedAsync(InterruptedError, cancellationToken);
                if (interrupted > 0)
                    _logger.LogWarning("{Count} WhatsApp message(s) were still queued when the app last stopped and are marked failed", interrupted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not mark the WhatsApp messages interrupted by the last stop as failed");
            }

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var job in _outbox.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await SendAsync(job, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        await RecordAsync(job, WhatsAppStatus.Failed, InterruptedError);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WhatsApp outbox failed for inventory code {Code}", job.InventoryCode);
                        await RecordAsync(job, WhatsAppStatus.Failed, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }

            // Messages still waiting are lost with the process, so their routes say so and offer Send again
            while (_outbox.Reader.TryRead(out var waiting))
                await RecordAsync(waiting, WhatsAppStatus.Failed, InterruptedError);
        }

        private async Task SendAsync(WhatsAppJob job, CancellationToken cancellationToken)
        {
            var groupId = _outbox.GroupId;
            if (string.IsNullOrEmpty(groupId)) return;   // Switched off meanwhile

            var image = await _images.ReadAsync(job.ImageUrl, cancellationToken);
            var fileName = Path.GetFileName(job.ImageUrl) ?? "";
            string? uploaded = null, lastError = null;
            var otherErrors = 0;

            await using var scope = _scopes.CreateAsyncScope();
            var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var result = await _outbox.PacedAsync(
                    () => whatsApp.SendAsync(groupId, job.Message, image, fileName, uploaded, cancellationToken), cancellationToken);
                // Retries reuse the uploaded image instead of uploading it again
                uploaded ??= result.UploadedImageUrl;

                if (result.Success)
                {
                    await RecordAsync(job, WhatsAppStatus.Sent, null, result.MessageId);
                    return;
                }

                lastError = result.Error;
                // Sending again could post the message twice, which also risks the account
                if (result.OutcomeUnknown) break;
                // A rate limit waits as asked and does not use up one of the MaxOtherErrors tries
                if (result.RateLimited)
                {
                    await Task.Delay((result.RetryAfter ?? _outbox.MinInterval) + TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }
                if (++otherErrors >= MaxOtherErrors) break;
                await Task.Delay(TimeSpan.FromSeconds(10 * otherErrors), cancellationToken);
            }

            _logger.LogWarning("WhatsApp message for inventory code {Code} was not delivered: {Error}", job.InventoryCode, lastError);
            await RecordAsync(job, WhatsAppStatus.Failed, lastError);
        }

        /// <summary>Stores the outcome on the job's route: the completed transfer, or the entry route of a new product.</summary>
        private async Task RecordAsync(WhatsAppJob job, string status, string? error, long? messageId = null)
        {
            if (job.RouteId is not int routeId) return;
            // Not cancelled by a stop and tried again after a failure, or the route would stay queued with nothing left to send
            for (var attempt = 1; attempt <= RecordAttempts; attempt++)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IRouteWhatsAppStatus>().SetAsync(routeId, status, error, messageId);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not record WhatsApp status {Status} for route {RouteId} (attempt {Attempt})", status, routeId, attempt);
                    if (attempt < RecordAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
        }
    }

    /// <summary>Queues WhatsApp messages for new products, completed transfers and resends from the route pages, and deletes sent ones.</summary>
    public sealed class WhatsAppRouteNotifier : IWhatsAppRouteNotifier
    {
        private const int MaxDeleteAttempts = 3;

        private readonly WhatsAppOutbox _outbox;
        private readonly IWhatsAppService _whatsApp;
        private readonly IConfiguration _configuration;

        public WhatsAppRouteNotifier(WhatsAppOutbox outbox, IWhatsAppService whatsApp, IConfiguration configuration)
        {
            _outbox = outbox;
            _whatsApp = whatsApp;
            _configuration = configuration;
        }

        public bool Enabled => _outbox.Enabled;

        // WhatsApp lets a message be deleted for everyone for about two days after sending
        public TimeSpan DeleteWindow => TimeSpan.FromHours(_configuration.GetValue("WhatsApp:DeleteWithinHours", 48.0));

        public Task QueueRouteCompletedAsync(RouteCompletedEvent e, CancellationToken cancellationToken = default)
        {
            Queue(new WhatsAppProductNotification
            {
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
            }, e.RouteId);
            return Task.CompletedTask;
        }

        public Task QueueProductCreatedAsync(ProductCreatedEvent e, int routeId, CancellationToken cancellationToken = default)
        {
            Queue(ProductCreatedMessage(e), routeId);
            return Task.CompletedTask;
        }

        public async Task<string?> DeleteMessageAsync(long messageId, CancellationToken cancellationToken = default)
        {
            WhatsAppDeleteResult result;
            var attempt = 0;
            do
            {
                result = await _outbox.PacedAsync(() => _whatsApp.DeleteAsync(messageId, cancellationToken), cancellationToken);
            }
            // A rate limit only means a message went out a moment ago, so the next paced call goes through
            while (!result.Success && result.RateLimited && ++attempt < MaxDeleteAttempts);
            return result.Success ? null : result.Error;
        }

        /// <summary>The group message for a new product.</summary>
        public static WhatsAppProductNotification ProductCreatedMessage(ProductCreatedEvent e) => new()
        {
            InventoryCode = e.Product.InventoryCode,
            Model = e.Product.Model,
            Vendor = e.Product.Vendor,
            CategoryName = e.Product.CategoryName,
            ToDepartmentName = e.Product.DepartmentName,
            ToWorker = e.Product.Worker,
            CreatedAt = e.CreatedAt,
            IsNewItem = e.Product.IsNewItem,
            IsWorking = e.Product.IsWorking,
            Notes = e.Product.Description,
            NotificationType = "created",
            ImageUrl = e.Product.ImageUrl
        };

        public void Queue(WhatsAppProductNotification notification, int? routeId)
        {
            if (!_outbox.Enabled) return;
            _outbox.Enqueue(new WhatsAppJob(_whatsApp.FormatNotification(notification), notification.ImageUrl,
                notification.InventoryCode, routeId));
        }
    }
}
