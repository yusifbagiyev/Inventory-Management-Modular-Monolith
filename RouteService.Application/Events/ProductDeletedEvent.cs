namespace RouteService.Application.Events
{
    public record ProductDeletedEvent
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public string? Model { get; set; }
        public string? Vendor { get; set; }
        public string? CategoryName { get; set; }
        public int DepartmentId { get; set; }
        public string? Worker { get; set; }
        public bool IsWorking { get; set; }
        public DateTime DeletedAt { get; set; }
    }
}
