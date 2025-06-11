using Microsoft.AspNetCore.Http;

namespace ProductService.Application.DTOs
{
    public record CreateProductDto
    {
        public int InventoryCode { get; set; }
        public string? Model { get; set; } = string.Empty;
        public string? Vendor { get; set; } = string.Empty;
        public IFormFile? ImageFile { get; set; }  // New property
        public string? Worker { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public bool IsWorking { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool IsNewItem { get; set; } = true;
        public int CategoryId { get; set; }
        public int DepartmentId { get; set; }
    }
}