using SharedServices.Events;

namespace NotificationService.Application.Interfaces
{
    /// <summary>Turns events into stored notifications, SignalR pushes and WhatsApp messages.</summary>
    /// <remarks>Runs on the background worker after the transaction has committed.</remarks>
    public interface INotificationDispatcher
    {
        Task ApprovalRequestCreatedAsync(ApprovalRequestCreatedEvent e, CancellationToken cancellationToken);
        Task ApprovalRequestProcessedAsync(ApprovalRequestProcessedEvent e, CancellationToken cancellationToken);
        Task ApprovalRequestCancelledAsync(ApprovalRequestCancelledEvent e, CancellationToken cancellationToken);
        // These skip actorId, since nobody needs a notification for what they just did.
        Task ProductCreatedAsync(ProductCreatedEvent e, int? actorId, CancellationToken cancellationToken);
        Task ProductDeletedAsync(ProductDeletedEvent e, int? actorId, CancellationToken cancellationToken);
        Task RouteCompletedAsync(RouteCompletedEvent e, int? actorId, CancellationToken cancellationToken);
    }
}
