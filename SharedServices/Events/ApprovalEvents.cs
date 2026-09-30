using MediatR;

namespace SharedServices.Events
{
    public record ApprovalRequestCreatedEvent(
        int RequestId,
        string RequestType,
        int RequestedById,
        string RequestedByName,
        DateTime CreatedAt) : INotification;

    /// <param name="Status">"Approved", "Rejected" or "Failed".</param>
    /// <param name="Reason">Rejection reason or execution error.</param>
    public record ApprovalRequestProcessedEvent(
        int RequestId,
        string RequestType,
        string Status,
        int ProcessedById,
        string ProcessedByName,
        int RequestedById,
        string? Reason) : INotification;

    public record ApprovalRequestCancelledEvent(
        int RequestId,
        string RequestType,
        int RequestedById,
        DateTime CancelledAt) : INotification;
}
