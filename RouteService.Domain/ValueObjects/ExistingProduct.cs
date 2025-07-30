namespace RouteService.Domain.ValueObjects
{
    public record ExistingProduct
    {
        public int ProductId { get; set; }
        public int InventoryCode { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; } = string.Empty;
        public string? Worker { get; set; } = string.Empty;
    }
}