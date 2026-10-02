using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Services;
using SharedServices.LiveUpdates;

namespace NotificationService.Infrastructure.Services
{
    /// <summary>Broadcasts committed changes as EntityChanged without record data, so pages re-fetch with the viewer's permissions.</summary>
    public sealed class LiveUpdatePublisher : ILiveUpdatePublisher
    {
        private readonly IHubContext<NotificationHub> _hub;

        public LiveUpdatePublisher(IHubContext<NotificationHub> hub) => _hub = hub;

        public Task PublishAsync(LiveUpdate update, CancellationToken cancellationToken)
            => _hub.Clients.All.SendAsync("EntityChanged", update, cancellationToken);
    }
}
