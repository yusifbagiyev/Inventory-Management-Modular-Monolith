namespace RouteService.Application.DTOs
{
    public record ProductInfoDto
    {
        public int Id { get; set; }
        public int InventoryCode { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public bool IsWorking { get; set; }
        public string? Worker { get; set; }
        public string? ImageUrl { get; set; }
    }
}
