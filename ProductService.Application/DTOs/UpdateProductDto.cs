using Microsoft.AspNetCore.Http;

namespace ProductService.Application.DTOs
{
    public record UpdateProductDto
    {
        public string? Model { get; set; } = string.Empty;
        public string? Vendor { get; set; } = string.Empty;
        public string? Worker { get; set; } = string.Empty;
        public IFormFile? ImageFile { get; set; }
        public string? Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int DepartmentId { get; set; }
        public bool IsWorking { get; set; }
        public bool IsActive { get; set; }
        public bool IsNewItem { get; set; }
    }
}