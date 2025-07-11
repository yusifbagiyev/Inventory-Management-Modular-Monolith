namespace NotificationService.Domain.Events
{
    public record RouteCreatedEvent
    {
        public int RouteId { get; set; }
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public int FromDepartmentId { get; set; }
        public string FromDepartmentName { get; set; } = string.Empty;
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public record RouteCompletedEvent
    {
        public int RouteId { get; set; }
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public int FromDepartmentId { get; set; }
        public string FromDepartmentName { get; set; } = string.Empty;
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
    }
}