namespace NotificationService.Application.Events
{
    public record ApprovalRequestCancelledEvent
    {
        public int RequestId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public DateTime CancelledAt { get; set; }
    }
}