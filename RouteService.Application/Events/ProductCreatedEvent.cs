namespace RouteService.Application.Events
{
    public record ProductCreatedEvent
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string Worker { get; set; } = string.Empty;
        public bool IsWorking { get; set; }
        public bool IsNewItem { get; set; }
        public byte[]? ImageData { get; set; }
        public string? ImageFileName { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}