namespace RouteService.Application.Events
{
    public record RouteCompletedEvent
    {
        public int RouteId { get; init; }
        public int ProductId { get; init; }
        public int InventoryCode { get; init; }
        public string Model { get; init; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int FromDepartmentId { get; init; }
        public string FromDepartmentName { get; init; } = string.Empty;
        public int ToDepartmentId { get; init; }
        public string ToDepartmentName { get; init; } = string.Empty;
        public string? FromWorker { get; set; } = string.Empty;
        public string ToWorker { get; init; } = string.Empty;
        public string? Notes { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageFileName { get; set; }
        public DateTime CompletedAt { get; init; }
    }
}