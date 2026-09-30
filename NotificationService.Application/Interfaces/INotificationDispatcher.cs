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
        Task ProductCreatedAsync(ProductCreatedEvent e, CancellationToken cancellationToken);
        Task ProductDeletedAsync(ProductDeletedEvent e, CancellationToken cancellationToken);
        Task RouteCompletedAsync(RouteCompletedEvent e, CancellationToken cancellationToken);
    }
}
