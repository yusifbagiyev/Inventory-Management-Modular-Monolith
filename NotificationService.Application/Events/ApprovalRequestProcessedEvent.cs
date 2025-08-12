namespace NotificationService.Application.Events
{
    public record ApprovalRequestProcessedEvent
    {
        public int RequestId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ProcessedById { get; set; }
        public string ProcessedByName { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string? RejectionReason { get; set; }
    }
}