using Microsoft.AspNetCore.SignalR;
using NotificationService.Application.Services;
using SharedServices.LiveUpdates;

namespace NotificationService.Infrastructure.Services
{
    /// <summary>
    /// Sends committed changes to every open page ("EntityChanged"). The message names only the
    /// kind of record, its id and who changed it; pages re-fetch with the viewer's own permissions.
    /// </summary>
    public sealed class LiveUpdatePublisher : ILiveUpdatePublisher
    {
        private readonly IHubContext<NotificationHub> _hub;

        public LiveUpdatePublisher(IHubContext<NotificationHub> hub) => _hub = hub;

        public Task PublishAsync(LiveUpdate update, CancellationToken cancellationToken)
            => _hub.Clients.All.SendAsync("EntityChanged", update, cancellationToken);
    }
}
