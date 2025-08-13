namespace NotificationService.Application.Events
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
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; }=string.Empty;
        public int FromDepartmentId { get; set; }
        public string FromDepartmentName { get; set; } = string.Empty;
        public int ToDepartmentId { get; set; }
        public string ToDepartmentName { get; set; } = string.Empty;
        public string? FromWorker { get; set; } = string.Empty;
        public string? ToWorker { get; set; } = string.Empty;
        public string? Notes {  get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageFileName { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}