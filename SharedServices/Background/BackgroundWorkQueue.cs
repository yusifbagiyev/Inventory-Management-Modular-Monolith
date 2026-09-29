using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedServices.Background
{
    /// <summary>Work executed in a fresh DI scope on the background worker.</summary>
    public delegate Task BackgroundWorkItem(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// In-process queue for work that must not block or fail the request that produced it
    /// (notification fan-out, WhatsApp). Items queued before a crash are lost.
    /// </summary>
    public sealed class BackgroundWorkQueue
    {
        private readonly Channel<BackgroundWorkItem> _channel =
            Channel.CreateUnbounded<BackgroundWorkItem>(new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(BackgroundWorkItem work) => _channel.Writer.TryWrite(work);

        internal ChannelReader<BackgroundWorkItem> Reader => _channel.Reader;
    }

    internal sealed class BackgroundWorkService : BackgroundService
    {
        private readonly BackgroundWorkQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundWorkService> _logger;

        public BackgroundWorkService(
            BackgroundWorkQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundWorkService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await work(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background work item failed");
                }
            }
        }
    }
}
