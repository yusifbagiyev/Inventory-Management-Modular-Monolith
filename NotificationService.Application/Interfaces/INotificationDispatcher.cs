using SharedServices.Events;

namespace NotificationService.Application.Interfaces
{
    /// <summary>
    /// Turns domain events into stored notifications, SignalR pushes and WhatsApp messages.
    /// Always invoked on the background worker after the originating transaction committed.
    /// </summary>
    public interface INotificationDispatcher
    {
        Task ApprovalRequestCreatedAsync(ApprovalRequestCreatedEvent e, CancellationToken cancellationToken);
        Task ApprovalRequestProcessedAsync(ApprovalRequestProcessedEvent e, CancellationToken cancellationToken);
        Task ApprovalRequestCancelledAsync(ApprovalRequestCancelledEvent e, CancellationToken cancellationToken);
        // Broadcasts to every user except <c>actorId</c>, the user whose request caused the event:
        // nobody needs a notification (and a sound) for what they just did themselves.
        Task ProductCreatedAsync(ProductCreatedEvent e, int? actorId, CancellationToken cancellationToken);
        Task ProductDeletedAsync(ProductDeletedEvent e, int? actorId, CancellationToken cancellationToken);
        Task RouteCompletedAsync(RouteCompletedEvent e, int? actorId, CancellationToken cancellationToken);
    }
}
