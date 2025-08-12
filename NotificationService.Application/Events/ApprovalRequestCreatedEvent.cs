namespace NotificationService.Application.Events
{
    public record ApprovalRequestCreatedEvent
    {
        public int RequestId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}