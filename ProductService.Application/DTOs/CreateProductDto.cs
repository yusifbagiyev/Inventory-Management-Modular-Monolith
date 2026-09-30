using Microsoft.AspNetCore.Http;

namespace ProductService.Application.DTOs
{
    public record CreateProductDto
    {
        public int InventoryCode { get; set; }
        public string? Model { get; set; } = string.Empty;
        public string? Vendor { get; set; } = string.Empty;
        /// <summary>Single image (older clients). Combined with <see cref="ImageFiles"/>, it comes first.</summary>
        public IFormFile? ImageFile { get; set; }
        /// <summary>Images in order; the first becomes the cover.</summary>
        public List<IFormFile>? ImageFiles { get; set; }
        public string? Worker { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public bool IsWorking { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public bool IsNewItem { get; set; } = true;
        public int CategoryId { get; set; }
        public int DepartmentId { get; set; }
    }
}