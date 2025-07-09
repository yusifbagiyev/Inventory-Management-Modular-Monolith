namespace NotificationService.Application.DTOs
{
    public record ApprovalRequestedEvent
    {
        public int ApprovalRequestId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public record ApprovalApprovedEvent
    {
        public int ApprovalRequestId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string ApprovedByName { get; set; } = string.Empty;
        public DateTime ApprovedAt { get; set; }
    }

    public record ApprovalRejectedEvent
    {
        public int ApprovalRequestId { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public int RequestedById { get; set; }
        public string RejectedByName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime RejectedAt { get; set; }
    }

    public record ProductCreatedEvent
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }

    public record ProductDeletedEvent
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
    }

    public record RouteCreatedEvent
    {
        public int RouteId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string ToDepartmentName { get; set; } = string.Empty;
    }

    public record RouteCompletedEvent
    {
        public int RouteId { get; set; }
    }
}