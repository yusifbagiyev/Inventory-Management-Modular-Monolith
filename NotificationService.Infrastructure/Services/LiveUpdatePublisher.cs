using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Services;
using SharedServices.LiveUpdates;

namespace NotificationService.Infrastructure.Services
{
    /// <summary>Sends committed changes to every open page as EntityChanged.</summary>
    /// <remarks>The message carries no record data. Pages re-fetch with the viewer's own permissions.</remarks>
    public sealed class LiveUpdatePublisher : ILiveUpdatePublisher
    {
        private readonly IHubContext<NotificationHub> _hub;

        public LiveUpdatePublisher(IHubContext<NotificationHub> hub) => _hub = hub;

        public Task PublishAsync(LiveUpdate update, CancellationToken cancellationToken)
            => _hub.Clients.All.SendAsync("EntityChanged", update, cancellationToken);
    }
}
