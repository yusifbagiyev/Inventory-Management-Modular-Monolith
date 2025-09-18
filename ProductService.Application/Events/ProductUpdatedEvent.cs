namespace ProductService.Application.Events
{
    public record ProductUpdatedEvent
    {
        public ExistingProduct Product { get; set; }=null!;
        public string? Changes { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; } = null;
        public string? ImageFileName { get; set; } = null;
        public DateTime UpdatedAt { get; set; }
    }
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