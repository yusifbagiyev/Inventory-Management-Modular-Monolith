using ApprovalService.Domain.Enums;

namespace ApprovalService.Domain.Entities
{
    /// <summary>A request as lists and dialogs read it, with ActionData free of the uploaded image bytes.</summary>
    public sealed record ApprovalRequestSummary
    {
        public int Id { get; init; }
        public string RequestType { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public int? EntityId { get; init; }
        public string ActionData { get; init; } = string.Empty;
        public int RequestedById { get; init; }
        public string RequestedByName { get; init; } = string.Empty;
        public int? ApprovedById { get; init; }
        public string? ApprovedByName { get; init; }
        public ApprovalStatus Status { get; init; }
        public string? RejectionReason { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime? ExecutedAt { get; init; }
    }
}
