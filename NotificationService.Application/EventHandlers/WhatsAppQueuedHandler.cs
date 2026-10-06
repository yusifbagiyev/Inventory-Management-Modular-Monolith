using MediatR;
using SharedServices.Contracts;
using SharedServices.Events;

namespace NotificationService.Application.EventHandlers
{
    /// <summary>Marks a completed route's WhatsApp message as queued in the completing transaction, so a message lost with the process still shows on the route.</summary>
    public class WhatsAppQueuedHandler : INotificationHandler<RouteCompletedEvent>
    {
        private readonly IWhatsAppRouteNotifier _whatsApp;
        private readonly IRouteWhatsAppStatus _status;

        public WhatsAppQueuedHandler(IWhatsAppRouteNotifier whatsApp, IRouteWhatsAppStatus status)
        {
            _whatsApp = whatsApp;
            _status = status;
        }

        public Task Handle(RouteCompletedEvent e, CancellationToken cancellationToken)
            => _whatsApp.Enabled
                ? _status.SetAsync(e.RouteId, WhatsAppStatus.Queued, null, cancellationToken)
                : Task.CompletedTask;
    }
}
