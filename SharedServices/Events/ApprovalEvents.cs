using MediatR;

namespace SharedServices.Events
{
    public record ApprovalRequestCreatedEvent(
        int RequestId,
        string RequestType,
        string RequestedByName) : INotification;

    // Status is Approved, Rejected or Failed, and Reason holds the rejection reason or the execution error
    public record ApprovalRequestProcessedEvent(
        int RequestId,
        string RequestType,
        string Status,
        string ProcessedByName,
        int RequestedById,
        string? Reason) : INotification;

    public record ApprovalRequestCancelledEvent(int RequestId) : INotification;
}
