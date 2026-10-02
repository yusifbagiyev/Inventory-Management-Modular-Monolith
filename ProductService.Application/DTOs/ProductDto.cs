namespace ProductService.Application.DTOs
{
    public record ProductDto
    {
        public int Id { get; set; }
        public int InventoryCode { get; set; }
        public string? Model { get; set; } = string.Empty;
        public string? Vendor { get; set; } = string.Empty;
        public string? ImageUrl { get; set; } = string.Empty;
        /// <summary>All images with the cover, equal to ImageUrl, first.</summary>
        public List<string> ImageUrls { get; set; } = [];
        public string? Description { get; set; } = string.Empty;
        public string? Worker { get; set; } = string.Empty;
        public bool IsWorking { get; set; }
        public bool IsActive { get; set; }
        public bool IsNewItem { get; set; }
        public string? Color { get; set; }
        public List<ProductSpecificationDto> Specifications { get; set; } = [];
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
        public string? DepartmentName { get; set; }=string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>Set only for deleted products.</summary>
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}